// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridge
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using Microsoft.Azure.Relay;

    public class TcpClientConnectionForwarder : IDisposable, IClientConnectionForwarder
    {
        readonly string bindTo;
        readonly object connectionLock = new object();
        readonly IDictionary<int, MultiplexedTcpConnection> connections;
        readonly object connectLock = new object();
        readonly Uri endpointVia;
        readonly IEnumerable<IPRange> firewallRules;
        readonly int fromPort;
        readonly TokenProvider tokenProvider;
        readonly int toPort;
        HybridConnectionStream dataChannel;
        HybridConnectionClient dataChannelFactory;
        MultiplexConnectionInputPump inputPump;
        QueueBufferedStream multiplexedOutputStream;
        StreamBufferWritePump outputPump;
        StreamBufferWritePump rawInputPump;
        TcpListener tcpListener;
        volatile bool open;

        public TcpClientConnectionForwarder(
            string serviceNamespace,
            string issuerName,
            string issuerSecret,
            string targetHost,
            int fromPort,
            int toPort,
            string bindTo,
            IEnumerable<IPRange> firewallRules)
        {
            this.toPort = toPort;
            this.fromPort = fromPort;
            this.bindTo = bindTo;
            this.firewallRules = firewallRules;

            connections = new Dictionary<int, MultiplexedTcpConnection>();
            endpointVia = new UriBuilder("sb", serviceNamespace, -1, targetHost).Uri;
            tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(issuerName, issuerSecret);
        }

        public void Open()
        {
            try
            {
                IPAddress bindToAddress;
                if (string.IsNullOrEmpty(bindTo) || !IPAddress.TryParse(bindTo, out bindToAddress))
                {
                    bindToAddress = IPAddress.Any;
                }
                tcpListener = new TcpListener(bindToAddress, fromPort);

                tcpListener.Start();
                this.open = true;
                tcpListener.BeginAcceptTcpClient(ClientAccepted, null);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Unable to open listener: {0}", ex.Message);
                this.open = false;
                throw;
            }
        }

        public void Close()
        {
            try
            {
                this.open = false;
                tcpListener.Stop();
            }
            catch (Exception ex)
            {
                Trace.TraceError("Unable to stop listener: {0}", ex.Message);
                throw;
            }
        }

        public void Dispose()
        {
            this.open = false;
            lock (connectLock)
            {
                DataChannelClose();
            }
        }

        MultiplexedConnection CorrelateConnection(int connectionId, object state)
        {
            MultiplexedTcpConnection connection;
            lock (this.connectionLock)
            {
                connections.TryGetValue(connectionId, out connection);
            }

            return connection;
        }

        void ClientAccepted(IAsyncResult asyncResult)
        {
            bool didReschedule = false;
            try
            {
                TcpClient tcpConnection = tcpListener.EndAcceptTcpClient(asyncResult);
                if (tcpConnection != null)
                {
                    tcpListener.BeginAcceptTcpClient(ClientAccepted, null);
                    didReschedule = true;

                    try
                    {
                        bool endpointInPermittedRange = false;
                        IPEndPoint remoteIPEndpoint = (IPEndPoint)tcpConnection.Client.RemoteEndPoint;
                        foreach (IPRange range in firewallRules)
                        {
                            if (range.IsInRange(remoteIPEndpoint.Address))
                            {
                                endpointInPermittedRange = true;
                                break;
                            }
                        }
                        if (!endpointInPermittedRange)
                        {
                            Trace.TraceWarning("No matching firewall rule. Dropping connection from {0}", remoteIPEndpoint.Address);
                            tcpConnection.Close();
                        }
                        else
                        {
                            tcpConnection.NoDelay = true;
                            tcpConnection.LingerState.Enabled = false;
                            Stream socketStream = tcpConnection.GetStream();

                            EnsureConnection();

                            MultiplexedTcpConnection multiplexedConnection = new MultiplexedTcpConnection(tcpConnection, multiplexedOutputStream);
                            multiplexedConnection.Closed += MultiplexedConnectionClosed;
                            lock (connectionLock)
                            {
                                connections.Add(multiplexedConnection.Id, multiplexedConnection);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("Unable to establish connection: {0}", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failure accepting client: {0}", ex.Message);
                if (!didReschedule && this.open)
                {
                    tcpListener.BeginAcceptTcpClient(ClientAccepted, null);
                }
            }
        }

        void MultiplexedConnectionClosed(object sender, EventArgs e)
        {
            MultiplexedTcpConnection connection = (MultiplexedTcpConnection)sender;
            lock (connectionLock)
            {
                connections.Remove(connection.Id);
            }
        }

        void EnsureConnection()
        {
            lock (connectLock)
            {
                if (dataChannel == null)
                {
                    multiplexedOutputStream = new ThrottledQueueBufferedStream(5);

                    QueueBufferedStream multiplexedInputStream = new QueueBufferedStream();
                    dataChannelFactory = new HybridConnectionClient(endpointVia, tokenProvider);
                    dataChannel = dataChannelFactory.CreateConnectionAsync().GetAwaiter().GetResult();

                    try
                    {
                        var preambleWriter = new BinaryWriter(dataChannel);
                        preambleWriter.Write("tcp:" + toPort);

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
                        DataChannelClose();
                        throw;
                    }
                    catch (Exception ex)
                    {
                        dataChannelFactory = null;

                        Trace.TraceError("Unable to establish data channel: {0}", ex.Message);
                        DataChannelClose();
                        throw;
                    }
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
            foreach (MultiplexedTcpConnection connection in new List<MultiplexedTcpConnection>(connections.Values))
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