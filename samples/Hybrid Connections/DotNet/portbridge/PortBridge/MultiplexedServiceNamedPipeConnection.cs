// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridge
{
    using System;
    using System.Diagnostics;
    using System.IO.Pipes;

    class MultiplexedServiceNamedPipeConnection : MultiplexedConnection
    {
        readonly MultiplexConnectionOutputPump outputPump;
        NamedPipeClientStream pipeClient;
        StreamConnection streamConnection;

        public MultiplexedServiceNamedPipeConnection(StreamConnection streamConnection, NamedPipeClientStream pipeClient, int connectionId)
            : base(pipeClient.Write, connectionId)
        {
            this.streamConnection = streamConnection;
            this.pipeClient = pipeClient;

            outputPump = new MultiplexConnectionOutputPump(pipeClient.Read, streamConnection.Stream.Write, connectionId);
            outputPump.BeginRunPump(PumpCompleted, null);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (pipeClient != null)
            {
                try
                {
                    pipeClient.Close();
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Error closing client: {0}", ex.Message);
                }
                pipeClient = null;
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