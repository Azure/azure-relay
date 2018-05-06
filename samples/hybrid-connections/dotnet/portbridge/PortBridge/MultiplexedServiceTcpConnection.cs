// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridge
{
    using System;
    using System.Diagnostics;
    using System.Net.Sockets;

    class MultiplexedServiceTcpConnection : MultiplexedConnection
    {
        readonly MultiplexConnectionOutputPump outputPump;
        StreamConnection streamConnection;
        TcpClient tcpClient;

        public MultiplexedServiceTcpConnection(StreamConnection streamConnection, TcpClient tcpClient, int connectionId)
            : base(tcpClient.GetStream().Write, connectionId)
        {
            this.streamConnection = streamConnection;
            this.tcpClient = tcpClient;

            outputPump = new MultiplexConnectionOutputPump(tcpClient.GetStream().Read, streamConnection.Stream.Write, connectionId);
            outputPump.BeginRunPump(PumpCompleted, null);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (tcpClient != null)
            {
                try
                {
                    tcpClient.Close();
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Error closing client: {0}", ex.Message);
                }
                tcpClient = null;
            }
        }

        void PumpCompleted(IAsyncResult asyncResult)
        {
            try
            {
                MultiplexConnectionOutputPump.EndRunPump(asyncResult);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error in pump: {0}", ex.Message);
            }
            Dispose();
        }
    }
}