// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridge
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Sockets;

    sealed class MultiplexedTcpConnection : MultiplexedConnection
    {
        MultiplexConnectionOutputPump outputPump;
        TcpClient tcpClient;

        public MultiplexedTcpConnection(TcpClient tcpClient, Stream multiplexedOutputStream)
            : base(tcpClient.GetStream().Write)
        {
            this.tcpClient = tcpClient;
            outputPump = new MultiplexConnectionOutputPump(tcpClient.GetStream().Read, multiplexedOutputStream.Write, Id);
            outputPump.BeginRunPump(PumpComplete, null);
        }

        public event EventHandler Closed;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (tcpClient != null)
            {
                tcpClient.Close();
                tcpClient = null;
            }
            if (outputPump != null)
            {
                outputPump.Dispose();
                outputPump = null;
            }
        }

        void PumpComplete(IAsyncResult a)
        {
            try
            {
                MultiplexConnectionOutputPump.EndRunPump(a);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failure in multiplex pump: {0}", ex.Message);
            }

            this.Closed?.Invoke(this, EventArgs.Empty);

            Dispose();
        }
    }
}