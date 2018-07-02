// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Relay.Bond.Epoxy
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Sockets;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Bond;
    using global::Bond.Comm;
    using global::Bond.Comm.Epoxy;
    using global::Bond.Comm.Layers;
    using global::Bond.Comm.Service;
    using global::Bond.IO.Safe;
    using global::Bond.Protocols;
    using Microsoft.Azure.Relay;

    public class RelayEpoxyConnection : Connection, IRequestResponseConnection, IEventConnection
    {
        static readonly EpoxyConfig EmptyConfig = new EpoxyConfig();
        readonly ConnectionType connectionType;
        readonly Logger logger;
        readonly Metrics metrics;
        readonly HybridConnectionStream networkStream;
        readonly RelayEpoxyListener parentListener;
        readonly RelayEpoxyTransport parentTransport;
        readonly ResponseMap responseMap;
        readonly ServiceHost serviceHost;
        readonly CancellationTokenSource shutdownTokenSource;
        readonly TaskCompletionSource<bool> startTask;
        readonly TaskCompletionSource<bool> stopTask;
        readonly AsyncLock sendLock;
        Stopwatch duration;
        Error errorDetails;
        // this member is used to capture any handshake errors
        ProtocolError handshakeError;
        long prevConversationId;
        ProtocolErrorCode protocolError;
        State state;

        RelayEpoxyConnection(
            ConnectionType connectionType,
            RelayEpoxyTransport parentTransport,
            RelayEpoxyListener parentListener,
            ServiceHost serviceHost,
            HybridConnectionStream networkStream,
            Logger logger,
            Metrics metrics)
        {
            Debug.Assert(parentTransport != null);
            Debug.Assert(connectionType != ConnectionType.Server || parentListener != null, "Server connections must have a listener");
            Debug.Assert(serviceHost != null);
            Debug.Assert(networkStream != null);

            this.connectionType = connectionType;

            this.parentTransport = parentTransport;
            this.parentListener = parentListener;
            this.serviceHost = serviceHost;

            this.networkStream = networkStream;

            responseMap = new ResponseMap();
            state = State.Created;
            startTask = new TaskCompletionSource<bool>();
            stopTask = new TaskCompletionSource<bool>();
            shutdownTokenSource = new CancellationTokenSource();
            sendLock = new AsyncLock();

            // start at -1 or 0 so the first conversation ID is 1 or 2.
            prevConversationId = (connectionType == ConnectionType.Client) ? -1 : 0;

            //ConnectionMetrics.local_endpoint = LocalEndPoint.ToString();
            //ConnectionMetrics.remote_endpoint = RemoteEndPoint.ToString();

            this.logger = logger;
            this.metrics = metrics;
        }

        public Task FireEventAsync<TPayload>(string serviceName, string methodName, IMessage<TPayload> message)
        {
            EnsureCorrectState(State.Connected);
            return SendEventAsync(serviceName, methodName, message);
        }

        public async Task<IMessage<TResponse>> RequestResponseAsync<TRequest, TResponse>(
            string serviceName,
            string methodName,
            IMessage<TRequest> message,
            CancellationToken ct)
        {
            EnsureCorrectState(State.Connected);

            // TODO: cancellation
            IMessage response = await SendRequestAsync(serviceName, methodName, message);
            return response.Convert<TResponse>();
        }

        internal static RelayEpoxyConnection MakeClientConnection(
            RelayEpoxyTransport parentTransport,
            HybridConnectionStream networkStream,
            Logger logger,
            Metrics metrics)
        {
            const RelayEpoxyListener parentListener = null;

            return new RelayEpoxyConnection(
                ConnectionType.Client,
                parentTransport,
                parentListener,
                new ServiceHost(logger),
                networkStream,
                logger,
                metrics);
        }

        internal static RelayEpoxyConnection MakeServerConnection(
            RelayEpoxyTransport parentTransport,
            RelayEpoxyListener parentListener,
            ServiceHost serviceHost,
            HybridConnectionStream networkStream,
            Logger logger,
            Metrics metrics)
        {
            return new RelayEpoxyConnection(
                ConnectionType.Server,
                parentTransport,
                parentListener,
                serviceHost,
                networkStream,
                logger,
                metrics);
        }

        public override string ToString()
        {
            return $"{nameof(RelayEpoxyConnection)}({this.connectionType})";
        }

        internal static Frame MessageToFrame(
            ulong conversationId,
            string serviceName,
            string methodName,
            EpoxyMessageType type,
            IMessage message,
            IBonded layerData,
            Logger logger)
        {
            var frame = new Frame(logger);

            {
                var headers = new EpoxyHeaders
                {
                    conversation_id = conversationId,
                    message_type = type,
                    service_name = serviceName ?? string.Empty, // service_name is not nullable
                    method_name = methodName ?? string.Empty // method_name is not nullable
                };

                const int initialHeaderBufferSize = 150;
                var outputBuffer = new OutputBuffer(initialHeaderBufferSize);
                var fastWriter = new FastBinaryWriter<OutputBuffer>(outputBuffer);
                Serialize.To(fastWriter, headers);

                frame.Add(new Framelet(FrameletType.EpoxyHeaders, outputBuffer.Data));
            }

            if (layerData != null)
            {
                const int initialLayerDataBufferSize = 150;
                var outputBuffer = new OutputBuffer(initialLayerDataBufferSize);
                var compactWriter = new CompactBinaryWriter<OutputBuffer>(outputBuffer);
                compactWriter.WriteVersion();
                layerData.Serialize(compactWriter);
                frame.Add(new Framelet(FrameletType.LayerData, outputBuffer.Data));
            }

            {
                FrameletType frameletType = message.IsError ? FrameletType.ErrorData : FrameletType.PayloadData;
                IBonded userData = message.IsError ? message.Error : message.RawPayload;

                const int initialMessageBufferSize = 1024;
                var outputBuffer = new OutputBuffer(initialMessageBufferSize);
                var compactWriter = new CompactBinaryWriter<OutputBuffer>(outputBuffer);
                compactWriter.WriteVersion();
                userData.Serialize(compactWriter);

                frame.Add(new Framelet(frameletType, outputBuffer.Data));
            }

            return frame;
        }

        internal static Frame MakeConfigFrame(Logger logger)
        {
            var outputBuffer = new OutputBuffer(1);
            var fastWriter = new FastBinaryWriter<OutputBuffer>(outputBuffer);
            Serialize.To(fastWriter, EmptyConfig);

            var frame = new Frame(1, logger);
            frame.Add(new Framelet(FrameletType.EpoxyConfig, outputBuffer.Data));
            return frame;
        }

        internal static Frame MakeProtocolErrorFrame(ProtocolErrorCode errorCode, Error details, Logger logger)
        {
            var protocolError = new ProtocolError
            {
                error_code = errorCode,
                details = (details == null ? null : new Bonded<Error>(details))
            };

            var outputBuffer = new OutputBuffer(16);
            var fastWriter = new FastBinaryWriter<OutputBuffer>(outputBuffer);
            Serialize.To(fastWriter, protocolError);

            var frame = new Frame(1, logger);
            frame.Add(new Framelet(FrameletType.ProtocolError, outputBuffer.Data));
            return frame;
        }

        async Task<IMessage> SendRequestAsync<TPayload>(string serviceName, string methodName, IMessage<TPayload> request)
        {
            var conversationId = AllocateNextConversationId();
            var totalTime = Stopwatch.StartNew();
            var requestMetrics = Metrics.StartRequestMetrics(ConnectionMetrics);
            var sendContext = new RelayEpoxySendContext(this, ConnectionMetrics, requestMetrics);

            IBonded layerData = null;
            ILayerStack layerStack;
            Error layerError = parentTransport.GetLayerStack(requestMetrics.request_id, out layerStack);

            if (layerError == null)
            {
                layerError = LayerStackUtils.ProcessOnSend(
                    layerStack,
                    MessageType.REQUEST,
                    sendContext,
                    out layerData,
                    logger);
            }

            if (layerError != null)
            {
                logger.Site().Error(
                    "{0} Sending request {1}/{2}.{3} failed due to layer error (Code: {4}, Message: {5}).",
                    this,
                    conversationId,
                    serviceName,
                    methodName,
                    layerError.error_code,
                    layerError.message);
                return Message.FromError(layerError);
            }

            var frame = MessageToFrame(conversationId, serviceName, methodName, EpoxyMessageType.REQUEST, request, layerData, logger);

            logger.Site().Debug("{0} Sending request {1}/{2}.", this, conversationId, methodName);
            var responseTask = responseMap.Add(conversationId, layerStack);

            bool wasSent = await SendFrameAsync(frame);
            logger.Site().Debug(
                "{0} Sending request {1}/{2}.{3} {4}.",
                this,
                conversationId,
                serviceName,
                methodName,
                wasSent ? "succeeded" : "failed");

            if (!wasSent)
            {
                bool wasCompleted = responseMap.Complete(
                    conversationId,
                    Message.FromError(
                        new Error
                        {
                            error_code = (int) ErrorCode.TRANSPORT_ERROR,
                            message = "Request could not be sent"
                        }));

                if (!wasCompleted)
                {
                    logger.Site().Information(
                        "{0} Unsuccessfully sent request {1}/{2}.{3} still received response.",
                        this,
                        conversationId,
                        serviceName,
                        methodName);
                }
            }
            var message = await responseTask;

            Metrics.FinishRequestMetrics(requestMetrics, totalTime);
            metrics.Emit(requestMetrics);
            return message;
        }

        async Task SendReplyAsync(
            ulong conversationId,
            IMessage response,
            ILayerStack layerStack,
            RequestMetrics requestMetrics)
        {
            var sendContext = new RelayEpoxySendContext(this, ConnectionMetrics, requestMetrics);

            IBonded layerData;
            Error layerError = LayerStackUtils.ProcessOnSend(
                layerStack,
                MessageType.RESPONSE,
                sendContext,
                out layerData,
                logger);

            // If there was a layer error, replace the response with the layer error
            if (layerError != null)
            {
                logger.Site().Error(
                    "{0} Sending reply for conversation ID {1} failed due to layer error (Code: {2}, Message: {3}).",
                    this,
                    conversationId,
                    layerError.error_code,
                    layerError.message);

                // Set layer error as result of this Bond method call, replacing original response.
                // Since this error will be returned to client, cleanse out internal server error details, if any.
                response = Message.FromError(Errors.CleanseInternalServerError(layerError));
            }

            var frame = MessageToFrame(conversationId, null, null, EpoxyMessageType.RESPONSE, response, layerData, logger);
            logger.Site().Debug("{0} Sending reply for conversation ID {1}.", this, conversationId);

            bool wasSent = await SendFrameAsync(frame);
            logger.Site().Debug(
                "{0} Sending reply for conversation ID {1} {2}.",
                this,
                conversationId,
                wasSent ? "succeedeed" : "failed");
        }

        internal async Task SendEventAsync(string serviceName, string methodName, IMessage message)
        {
            var conversationId = AllocateNextConversationId();
            var totalTime = Stopwatch.StartNew();
            var requestMetrics = Metrics.StartRequestMetrics(ConnectionMetrics);
            var sendContext = new RelayEpoxySendContext(this, ConnectionMetrics, requestMetrics);

            IBonded layerData = null;
            ILayerStack layerStack;
            Error layerError = parentTransport.GetLayerStack(requestMetrics.request_id, out layerStack);

            if (layerError == null)
            {
                layerError = LayerStackUtils.ProcessOnSend(
                    layerStack,
                    MessageType.EVENT,
                    sendContext,
                    out layerData,
                    logger);
            }

            if (layerError != null)
            {
                logger.Site().Error(
                    "{0} Sending event {1}/{2}.{3} failed due to layer error (Code: {4}, Message: {5}).",
                    this,
                    conversationId,
                    serviceName,
                    methodName,
                    layerError.error_code,
                    layerError.message);
                return;
            }

            var frame = MessageToFrame(conversationId, serviceName, methodName, EpoxyMessageType.EVENT, message, layerData, logger);

            logger.Site().Debug("{0} Sending event {1}/{2}.{3}.", this, conversationId, serviceName, methodName);

            bool wasSent = await SendFrameAsync(frame);
            logger.Site().Debug(
                "{0} Sending event {1}/{2}.{3} {4}.",
                this,
                conversationId,
                serviceName,
                methodName,
                wasSent ? "succeeded" : "failed");

            Metrics.FinishRequestMetrics(requestMetrics, totalTime);
            metrics.Emit(requestMetrics);
        }

        async Task<bool> SendFrameAsync(Frame frame)
        {
            try
            {
                Stream stream = networkStream;
                using (await this.sendLock.LockAsync())
                {
                    await frame.WriteAsync(stream);
                    await stream.FlushAsync();
                }

                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is SocketException)
            {
                logger.Site().Error(ex, "{0} While writing a Frame to the network: {1}", this, ex.Message);
                return false;
            }
        }

        internal Task StartAsync()
        {
            EnsureCorrectState(State.Created);
            duration = Stopwatch.StartNew();
            Task.Run((Func<Task>) ConnectionLoop);
            return startTask.Task;
        }

        void EnsureCorrectState(State allowedStates, [CallerMemberName] string methodName = "<unknown>")
        {
            if ((state & allowedStates) == 0)
            {
                var message =
                    $"Connection ({this}) is not in the correct state for the requested operation ({methodName}). Current state: {state} Allowed states: {allowedStates}";
                throw new InvalidOperationException(message);
            }
        }

        ulong AllocateNextConversationId()
        {
            // Interlocked.Add() handles overflow by wrapping, not throwing.
            var newConversationId = Interlocked.Add(ref prevConversationId, 2);
            if (newConversationId < 0)
            {
                throw new EpoxyProtocolErrorException("Exhausted conversation IDs");
            }
            return unchecked((ulong) newConversationId);
        }

        async Task ConnectionLoop()
        {
            while (true)
            {
                State nextState;

                try
                {
                    if (state == State.Disconnected)
                    {
                        break; // while loop
                    }

                    switch (state)
                    {
                        case State.Created:
                            nextState = DoCreated();
                            break;

                        case State.ClientSendConfig:
                        case State.ServerSendConfig:
                            nextState = await DoSendConfigAsync();
                            break;

                        case State.ClientExpectConfig:
                        case State.ServerExpectConfig:
                            nextState = await DoExpectConfigAsync();
                            break;

                        case State.Connected:
                            // signal after state change to prevent races with
                            // EnsureCorrectState
                            startTask.SetResult(true);
                            nextState = await DoConnectedAsync();
                            break;

                        case State.SendProtocolError:
                            nextState = await DoSendProtocolErrorAsync();
                            break;

                        case State.Disconnecting:
                            nextState = DoDisconnect();
                            break;

                        case State.Disconnected: // we should never enter this switch in the Disconnected state
                        default:
                            logger.Site().Error("{0} Unexpected connection state: {1}", this, state);
                            protocolError = ProtocolErrorCode.INTERNAL_ERROR;
                            nextState = State.SendProtocolError;
                            break;
                    }
                }
                catch (Exception ex) when (state != State.Disconnecting && state != State.Disconnected)
                {
                    logger.Site().Error(ex, "{0} Unhandled exception. Current state: {1}", this, state);

                    // we're in a state where we can attempt to disconnect
                    protocolError = ProtocolErrorCode.INTERNAL_ERROR;
                    nextState = State.Disconnecting;
                }
                catch (Exception ex)
                {
                    logger.Site().Error(
                        ex,
                        "{0} Unhandled exception during shutdown. Abandoning connection. Current state: {1}",
                        this,
                        state);
                    break; // the while loop
                }

                state = nextState;
            } // while (true)

            if (state != State.Disconnected)
            {
                logger.Site().Information("{0} Abandoning connection. Current state: {1}", this, state);
            }

            DoDisconnected();
        }

        State DoCreated()
        {
            State result;

            if (connectionType == ConnectionType.Server)
            {
                var args = new ConnectedEventArgs(this);
                Error disconnectError = parentListener.InvokeOnConnected(args);

                if (disconnectError == null)
                {
                    result = State.ServerExpectConfig;
                }
                else
                {
                    logger.Site().Information(
                        "{0} Rejecting connection because {1}:{2}",
                        this,
                        disconnectError.error_code,
                        disconnectError.message);

                    protocolError = ProtocolErrorCode.CONNECTION_REJECTED;
                    errorDetails = disconnectError;
                    result = State.SendProtocolError;
                }
            }
            else
            {
                result = State.ClientSendConfig;
            }

            return result;
        }

        async Task<State> DoSendConfigAsync()
        {
            Frame emptyConfigFrame = MakeConfigFrame(logger);
            await SendFrameAsync(emptyConfigFrame);
            return (connectionType == ConnectionType.Server ? State.Connected : State.ClientExpectConfig);
        }

        async Task<State> DoExpectConfigAsync()
        {
            Stream stream = networkStream;
            Frame frame = await Frame.ReadAsync(stream, shutdownTokenSource.Token, logger);
            if (frame == null)
            {
                logger.Site().Information("{0} EOS encountered while waiting for config, so disconnecting.", this);
                return State.Disconnecting;
            }

            var result = EpoxyProtocol.Classify(frame, logger);
            switch (result.Disposition)
            {
                case EpoxyProtocol.FrameDisposition.ProcessConfig:
                    // we don't actually use the config yet
                    return (connectionType == ConnectionType.Server ? State.ServerSendConfig : State.Connected);

                case EpoxyProtocol.FrameDisposition.HandleProtocolError:
                    // we got a protocol error while we expected config
                    handshakeError = result.Error;
                    return State.Disconnecting;

                case EpoxyProtocol.FrameDisposition.HangUp:
                    return State.Disconnecting;

                default:
                    protocolError = result.ErrorCode ?? ProtocolErrorCode.PROTOCOL_VIOLATED;
                    logger.Site().Error(
                        "{0} Unsupported FrameDisposition {1} when waiting for config. ErrorCode: {2})",
                        this,
                        result.Disposition,
                        protocolError);
                    return State.SendProtocolError;
            }
        }

        async Task<State> DoConnectedAsync()
        {
            while (!shutdownTokenSource.IsCancellationRequested)
            {
                Frame frame;
                try
                {
                    Stream stream = networkStream;
                    frame = await Frame.ReadAsync(stream, shutdownTokenSource.Token, logger);
                    if (frame == null)
                    {
                        logger.Site().Information("{0} EOS encountered, so disconnecting.", this);
                        return State.Disconnecting;
                    }
                }
                catch (EpoxyProtocolErrorException pex)
                {
                    logger.Site().Error(pex, "{0} Protocol error encountered.", this);
                    protocolError = ProtocolErrorCode.PROTOCOL_VIOLATED;
                    return State.SendProtocolError;
                }
                catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is SocketException)
                {
                    logger.Site().Error(ex, "{0} IO error encountered.", this);
                    return State.Disconnecting;
                }

                var result = EpoxyProtocol.Classify(frame, logger);
                switch (result.Disposition)
                {
                    case EpoxyProtocol.FrameDisposition.DeliverRequestToService:
                    {
                        State? nextState = DispatchRequest(result.Headers, result.MessageData, result.LayerData);
                        if (nextState.HasValue)
                        {
                            return nextState.Value;
                        }
                        // continue the read loop
                        break;
                    }

                    case EpoxyProtocol.FrameDisposition.DeliverResponseToProxy:
                        DispatchResponse(result.Headers, result.MessageData, result.LayerData);
                        break;

                    case EpoxyProtocol.FrameDisposition.DeliverEventToService:
                        DispatchEvent(result.Headers, result.MessageData, result.LayerData);
                        break;

                    case EpoxyProtocol.FrameDisposition.SendProtocolError:
                        protocolError = result.ErrorCode ?? ProtocolErrorCode.INTERNAL_ERROR;
                        return State.SendProtocolError;

                    case EpoxyProtocol.FrameDisposition.HandleProtocolError:
                    case EpoxyProtocol.FrameDisposition.HangUp:
                        return State.Disconnecting;

                    default:
                        logger.Site().Error("{0} Unsupported FrameDisposition {1}", this, result.Disposition);
                        protocolError = ProtocolErrorCode.INTERNAL_ERROR;
                        return State.SendProtocolError;
                }
            }

            // shutdown requested between reading frames
            return State.Disconnecting;
        }

        async Task<State> DoSendProtocolErrorAsync()
        {
            ProtocolErrorCode errorCode = protocolError;
            Error details = errorDetails;

            var frame = MakeProtocolErrorFrame(errorCode, details, logger);
            logger.Site().Debug(
                "{0} Sending protocol error with code {1} and details {2}.",
                this,
                errorCode,
                details == null ? "<null>" : details.error_code + details.message);

            bool wasSent = await SendFrameAsync(frame);
            logger.Site().Debug(
                "{0} Sending protocol error with code {1} {2}.",
                this,
                errorCode,
                wasSent ? "succeeded" : "failed");

            return State.Disconnecting;
        }

        State DoDisconnect()
        {
            logger.Site().Debug("{0} Shutting down.", this);

            networkStream.Shutdown();

            if (connectionType == ConnectionType.Server)
            {
                var args = new DisconnectedEventArgs(this, errorDetails);
                parentListener.InvokeOnDisconnected(args);
            }

            responseMap.Shutdown();

            return State.Disconnected;
        }

        void DoDisconnected()
        {
            // We signal the start and stop tasks after the state change to
            // prevent races with EnsureCorrectState

            if (handshakeError != null)
            {
                var pex = new EpoxyProtocolErrorException(
                    "Connection was rejected",
                    null,
                    handshakeError.details);
                startTask.TrySetException(pex);
            }
            else
            {
                // the connection got started but then got shutdown shortly after
                startTask.TrySetResult(true);
            }

            stopTask.SetResult(true);

            duration.Stop();
            Metrics.FinishConnectionMetrics(ConnectionMetrics, duration);
            metrics.Emit(ConnectionMetrics);
        }

        State? DispatchRequest(EpoxyHeaders headers, EpoxyProtocol.MessageData messageData, ArraySegment<byte> layerData)
        {
            Task.Run(
                async () =>
                {
                    var totalTime = Stopwatch.StartNew();
                    var requestMetrics = Metrics.StartRequestMetrics(ConnectionMetrics);
                    var receiveContext = new RelayEpoxyReceiveContext(this, ConnectionMetrics, requestMetrics);

                    ILayerStack layerStack = null;

                    IMessage result;

                    if (messageData.IsError)
                    {
                        logger.Site().Error(
                            "{0} Received request with an error message. Only payload messages are allowed. Conversation ID: {1}",
                            this,
                            headers.conversation_id);
                        result = Message.FromError(
                            new Error
                            {
                                error_code = (int) ErrorCode.INVALID_INVOCATION,
                                message = "Received request with an error message"
                            });
                    }
                    else
                    {
                        IMessage request = Message.FromPayload(Unmarshal.From(messageData.Data));
                        IBonded bondedLayerData = (layerData.Array == null) ? null : Unmarshal.From(layerData);

                        Error layerError = parentTransport.GetLayerStack(requestMetrics.request_id, out layerStack);

                        if (layerError == null)
                        {
                            layerError = LayerStackUtils.ProcessOnReceive(
                                layerStack,
                                MessageType.REQUEST,
                                receiveContext,
                                bondedLayerData,
                                logger);
                        }

                        if (layerError == null)
                        {
                            result = await serviceHost.DispatchRequest(headers.service_name, headers.method_name, receiveContext, request);
                        }
                        else
                        {
                            logger.Site().Error(
                                "{0} Receiving request {1}/{2}.{3} failed due to layer error (Code: {4}, Message: {5}).",
                                this,
                                headers.conversation_id,
                                headers.service_name,
                                headers.method_name,
                                layerError.error_code,
                                layerError.message);

                            // Set layer error as result of this Bond method call and do not dispatch to method.
                            // Since this error will be returned to client, cleanse out internal server error details, if any.
                            result = Message.FromError(Errors.CleanseInternalServerError(layerError));
                        }
                    }

                    await SendReplyAsync(headers.conversation_id, result, layerStack, requestMetrics);
                    Metrics.FinishRequestMetrics(requestMetrics, totalTime);
                    metrics.Emit(requestMetrics);
                });

            // no state change needed
            return null;
        }

        void DispatchResponse(EpoxyHeaders headers, EpoxyProtocol.MessageData messageData, ArraySegment<byte> layerData)
        {
            IMessage response = messageData.IsError
                ? Message.FromError(Unmarshal<Error>.From(messageData.Data))
                : Message.FromPayload(Unmarshal.From(messageData.Data));

            TaskCompletionSource<IMessage> tcs = responseMap.TakeTaskCompletionSource(headers.conversation_id);
            if (tcs == null)
            {
                logger.Site().Error(
                    "{0} Response for unmatched request. Conversation ID: {1}",
                    this,
                    headers.conversation_id);
                return;
            }

            Task.Run(
                () =>
                {
                    var totalTime = Stopwatch.StartNew();
                    var requestMetrics = Metrics.StartRequestMetrics(ConnectionMetrics);
                    var receiveContext = new RelayEpoxyReceiveContext(this, ConnectionMetrics, requestMetrics);

                    IBonded bondedLayerData = (layerData.Array == null) ? null : Unmarshal.From(layerData);

                    ILayerStack layerStack = tcs.Task.AsyncState as ILayerStack;

                    Error layerError = LayerStackUtils.ProcessOnReceive(layerStack, MessageType.RESPONSE, receiveContext, bondedLayerData, logger);

                    if (layerError != null)
                    {
                        logger.Site().Error(
                            "{0} Receiving response {1}/{2}.{3} failed due to layer error (Code: {4}, Message: {5}).",
                            this,
                            headers.conversation_id,
                            headers.service_name,
                            headers.method_name,
                            layerError.error_code,
                            layerError.message);
                        response = Message.FromError(layerError);
                    }

                    tcs.SetResult(response);
                    Metrics.FinishRequestMetrics(requestMetrics, totalTime);
                    metrics.Emit(requestMetrics);
                });
        }

        void DispatchEvent(EpoxyHeaders headers, EpoxyProtocol.MessageData messageData, ArraySegment<byte> layerData)
        {
            if (messageData.IsError)
            {
                logger.Site().Error(
                    "{0} Received event with an error message. Only payload messages are allowed. Conversation ID: {1}",
                    this,
                    headers.conversation_id);
                return;
            }

            Task.Run(
                async () =>
                {
                    IMessage request = Message.FromPayload(Unmarshal.From(messageData.Data));
                    var totalTime = Stopwatch.StartNew();
                    var requestMetrics = Metrics.StartRequestMetrics(ConnectionMetrics);
                    var receiveContext = new RelayEpoxyReceiveContext(this, ConnectionMetrics, requestMetrics);

                    IBonded bondedLayerData = (layerData.Array == null) ? null : Unmarshal.From(layerData);
                    ILayerStack layerStack;
                    Error layerError = parentTransport.GetLayerStack(requestMetrics.request_id, out layerStack);

                    if (layerError == null)
                    {
                        layerError = LayerStackUtils.ProcessOnReceive(
                            layerStack,
                            MessageType.EVENT,
                            receiveContext,
                            bondedLayerData,
                            logger);
                    }

                    if (layerError != null)
                    {
                        logger.Site().Error(
                            "{0}: Receiving event {1}/{2}.{3} failed due to layer error (Code: {4}, Message: {5}).",
                            this,
                            headers.conversation_id,
                            headers.service_name,
                            headers.method_name,
                            layerError.error_code,
                            layerError.message);
                        return;
                    }

                    await serviceHost.DispatchEvent(headers.service_name, headers.method_name, receiveContext, request);
                    Metrics.FinishRequestMetrics(requestMetrics, totalTime);
                    metrics.Emit(requestMetrics);
                });
        }

        public override Task StopAsync()
        {
            EnsureCorrectState(State.All);
            networkStream.Shutdown();
            shutdownTokenSource.Cancel();

            return stopTask.Task;
        }

        enum ConnectionType
        {
            Client,
            Server
        }

        [Flags]
        enum State
        {
            None = 0,
            Created = 0x01,
            ClientSendConfig = 0x02,
            ClientExpectConfig = 0x04,
            ServerExpectConfig = 0x08,
            ServerSendConfig = 0x10,
            Connected = 0x20,
            SendProtocolError = 0x40,
            Disconnecting = 0x80,
            Disconnected = 0x100,

            All =
                Created | ClientSendConfig | ClientExpectConfig | ServerExpectConfig | ServerSendConfig | Connected | SendProtocolError |
                Disconnecting | Disconnected
        }
    }
}