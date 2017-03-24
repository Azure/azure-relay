// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridge
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Pipes;
    using Microsoft.Azure.Relay;

    public class NamedPipeClientConnectionForwarder : IDisposable, IClientConnectionForwarder
    {
        readonly object connectionLock = new object();
        readonly Dictionary<int, MultiplexedPipeConnection> connections;
        readonly object connectLock = new object();
        readonly Uri endpointVia;
        readonly string localPipe;
        readonly TokenProvider tokenProvider;
        readonly string toPipe;
        HybridConnectionStream dataChannel;
        HybridConnectionClient dataChannelFactory;
        MultiplexConnectionInputPump inputPump;
        QueueBufferedStream multiplexedOutputStream;
        StreamBufferWritePump outputPump;
        StreamBufferWritePump rawInputPump;

        public NamedPipeClientConnectionForwarder(
            string serviceNamespace,
            string keyName,
            string key,
            string targetHost,
            string localPipe,
            string toPipe)
        {
            this.toPipe = toPipe;
            this.localPipe = localPipe;

            connections = new Dictionary<int, MultiplexedPipeConnection>();
            endpointVia = new UriBuilder("sb", serviceNamespace, -1, targetHost).Uri;
            tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(keyName, key);
        }

        public void Open()
        {
            try
            {
                PipeSecurity pipeSecurity = new PipeSecurity();
                // deny network access, allow read/write to everyone locally and the owner/creator
                pipeSecurity.SetSecurityDescriptorSddlForm("D:(D;;FA;;;NU)(A;;0x12019f;;;WD)(A;;0x12019f;;;CO)");

                NamedPipeServerStream pipeListener =
                    new NamedPipeServerStream(
                        localPipe,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous,
                        4096,
                        4096,
                        pipeSecurity);
                pipeListener.BeginWaitForConnection(ClientAccepted, pipeListener);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Unable to open listener: {0}", ex.Message);
                throw;
            }
        }

        public void Close()
        {
            try
            {
            }
            catch (Exception ex)
            {
                Trace.TraceError("Unable to stop listener: {0}", ex.Message);
                throw;
            }
        }

        public void Dispose()
        {
            lock (connectLock)
            {
                DataChannelClose();
            }
        }

        MultiplexedConnection CorrelateConnection(int connectionId, object state)
        {
            MultiplexedPipeConnection connection = null;
            connections.TryGetValue(connectionId, out connection);
            return connection;
        }

        void ClientAccepted(IAsyncResult asyncResult)
        {
            try
            {
                NamedPipeServerStream pipeListener = asyncResult.AsyncState as NamedPipeServerStream;
                pipeListener.EndWaitForConnection(asyncResult);

                NamedPipeServerStream nextPipeListener = new NamedPipeServerStream(
                    localPipe,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous);
                nextPipeListener.BeginWaitForConnection(ClientAccepted, nextPipeListener);

                try
                {
                    Stream socketStream = pipeListener;

                    EnsureConnection();

                    MultiplexedPipeConnection multiplexedConnection = new MultiplexedPipeConnection(pipeListener, multiplexedOutputStream);
                    multiplexedConnection.Closed += MultiplexedConnectionClosed;
                    lock (connectionLock)
                    {
                        connections.Add(multiplexedConnection.Id, multiplexedConnection);
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Unable to establish connection: {0}", ex.Message);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failure accepting client: {0}", ex.Message);
            }
        }

        void MultiplexedConnectionClosed(object sender, EventArgs e)
        {
            MultiplexedPipeConnection connection = (MultiplexedPipeConnection)sender;
            connections.Remove(connection.Id);
        }

        void EnsureConnection()
        {
            lock (connectLock)
            {
                if (dataChannel == null)
                {
                    multiplexedOutputStream = new ThrottledQueueBufferedStream(10);

                    QueueBufferedStream multiplexedInputStream = new QueueBufferedStream();

                    dataChannelFactory = new HybridConnectionClient(endpointVia, tokenProvider);
                    dataChannel = dataChannelFactory.CreateConnectionAsync().GetAwaiter().GetResult();

                    try
                    {
                        var preambleWriter = new BinaryWriter(dataChannel);
                        preambleWriter.Write("np:" + toPipe);

                        rawInputPump = new StreamBufferWritePump(dataChannel, multiplexedInputStream.Write);
                        rawInputPump.BeginRunPump(RawInputPumpCompleted, false);

                        inputPump = new MultiplexConnectionInputPump(multiplexedInputStream.Read, CorrelateConnection, null);
                        inputPump.Run(false);

                        outputPump = new StreamBufferWritePump(multiplexedOutputStream, WriteToDataChannel);
                        outputPump.BeginRunPump(MultiplexPumpCompleted, null);
                    }
                    catch (AuthorizationFailedException af)
                    {
                        Trace.TraceError("Authorization failed: {0}", af.Message);
                        if (dataChannel != null)
                        {
                            DataChannelClose();
                            dataChannel = null;
                        }
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("Unable to establish data channel: {0}", ex.Message);
                        if (dataChannel != null)
                        {
                            DataChannelClose();
                            dataChannel = null;
                        }
                        throw;
                    }
                }
            }
        }

        void RawInputPumpCompleted(IAsyncResult a)
        {
            try
            {
                try
                {
                    Pump.EndRunPump(a);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Multiplex pump failed: {0}", ex.Message);
                }

                lock (connectLock)
                {
                    dataChannel?.Shutdown();
                    DataChannelClose();
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error closing data channel: {0}", ex.Message);
                lock (connectLock)
                {
                    DataChannelClose();
                }
            }
        }

        void MultiplexPumpCompleted(IAsyncResult a)
        {
            try
            {
                try
                {
                    Pump.EndRunPump(a);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Multiplex pump failed: {0}", ex.Message);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error closing data channel: {0}", ex.Message);
            }
        }

        void WriteToDataChannel(byte[] b, int o, int s)
        {
            lock (connectLock)
            {
                try
                {
                    var bw = new BinaryWriter(dataChannel);
                    bw.Write(b, o, s);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Failed writing to data channel: {0}", ex.Message);

                    DataChannelClose();
                    throw;
                }
            }
        }

        void DataChannelClose()
        {
            foreach (MultiplexedPipeConnection connection in new List<MultiplexedPipeConnection>(connections.Values))
            {
                try
                {
                    lock (connectionLock)
                    {
                        connections.Remove(connection.Id);
                    }
                    connection.Dispose();
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Error shutting down multiplex connection: {0}", ex.Message);
                }
            }
            if (dataChannel != null)
            {
                dataChannel.Close();
                dataChannel = null;
            }
        }
    }
}