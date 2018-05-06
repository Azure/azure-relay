// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridge
{
    using System;
    using System.Diagnostics;
    using System.IO.Pipes;

    class MultiplexedPipeConnection : MultiplexedConnection
    {
        MultiplexConnectionOutputPump outputPump;
        NamedPipeServerStream pipeServer;

        public MultiplexedPipeConnection(NamedPipeServerStream pipeServer, QueueBufferedStream multiplexedOutputStream)
            : base(pipeServer.Write)
        {
            this.pipeServer = pipeServer;
            outputPump = new MultiplexConnectionOutputPump(pipeServer.Read, multiplexedOutputStream.Write, Id);
            outputPump.BeginRunPump(PumpComplete, null);
        }

        public event EventHandler Closed;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (pipeServer != null)
            {
                pipeServer.Close();
                pipeServer = null;
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

            if (Closed != null)
            {
                Closed(this, new EventArgs());
            }

            Dispose();
        }
    }
}