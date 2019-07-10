// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridge
{
    using System;
    using System.Threading.Tasks;

    public class BufferPump : Pump
    {
        readonly BufferRead bufferRead;
        readonly BufferWrite bufferWrite;
        readonly byte[] inputBuffer;

        public BufferPump(BufferRead bufferRead, BufferWrite bufferWrite)
            : this(bufferRead, bufferWrite, 65536)
        {
        }

        public BufferPump(BufferRead bufferRead, BufferWrite bufferWrite, int bufferSize)
        {
            this.bufferRead = bufferRead;
            this.bufferWrite = bufferWrite;
            inputBuffer = new byte[bufferSize];
        }

        public override IAsyncResult BeginRunPump(AsyncCallback callback, object state)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("Already running");
            }
            IsRunning = true;

            Caller = new PumpAsyncResult(callback, state);

            Task.Run(() => bufferRead(inputBuffer, 0, inputBuffer.Length)).ContinueWith(DoneReading);

            return Caller;
        }

        public void DoneReading(Task<int> bufferReadTask)
        {
            try
            {
                int bytesRead = bufferReadTask.GetAwaiter().GetResult();
                if (bytesRead > 0)
                {
                    bufferWrite(inputBuffer, 0, bytesRead);
                    if (!IsClosed)
                    {
                        Task.Run(() => bufferRead(inputBuffer, 0, inputBuffer.Length)).ContinueWith(DoneReading);
                    }
                }
                else
                {
                    SetComplete();
                }
            }
            catch (Exception ex)
            {
                SetComplete(ex);
            }
        }
    }
}