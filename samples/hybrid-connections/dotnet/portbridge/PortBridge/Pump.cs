// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridge
{
    using System;

    public abstract class Pump : IDisposable
    {
        internal PumpAsyncResult Caller { get; set; }
        internal bool IsRunning { get; set; }
        protected bool IsClosed { get; set; }

        public void Dispose()
        {
            if (!IsClosed)
            {
                IsClosed = true;
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }

        ~Pump()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public abstract IAsyncResult BeginRunPump(AsyncCallback callback, object state);

        public static void EndRunPump(IAsyncResult asyncResult)
        {
            PumpAsyncResult.End(asyncResult);
        }

        public void RunPump()
        {
            EndRunPump(BeginRunPump(null, null));
        }

        protected void SetComplete()
        {
            Caller.SetComplete();
            Dispose();
        }

        protected void SetComplete(Exception ex)
        {
            Caller.SetComplete(ex);
            Dispose();
        }

        internal class PumpAsyncResult : AsyncResult
        {
            public PumpAsyncResult(AsyncCallback callback, object state)
                : base(callback, state)
            {
            }

            internal static void End(IAsyncResult asyncResult)
            {
                End<PumpAsyncResult>(asyncResult);
            }

            internal void SetComplete()
            {
                Complete(false);
            }

            internal void SetComplete(Exception ex)
            {
                Complete(false, ex);
            }
        }
    }
}