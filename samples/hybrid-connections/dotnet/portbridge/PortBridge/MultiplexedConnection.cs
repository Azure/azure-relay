// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridge
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    public abstract class MultiplexedConnection : IDisposable
    {
        static int lastConnection;
        readonly BufferWrite bufferWrite;

        public MultiplexedConnection(BufferWrite bufferWrite)
            : this(bufferWrite, Interlocked.Increment(ref lastConnection))
        {
        }

        public MultiplexedConnection(BufferWrite bufferWrite, int connectionId)
        {
            Id = connectionId;
            this.bufferWrite = bufferWrite ?? throw new ArgumentNullException(nameof(bufferWrite));
            Trace.TraceInformation("Connection {0} created", connectionId);
        }

        public int Id { get; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            Trace.TraceInformation("Connection {0} completed", Id);
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            bufferWrite(buffer, offset, count);
        }
    }
}
