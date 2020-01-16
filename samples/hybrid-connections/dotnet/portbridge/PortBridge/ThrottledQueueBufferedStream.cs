// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridge
{
    using System;
    using System.Threading;

    public class ThrottledQueueBufferedStream : QueueBufferedStream
    {
        readonly Semaphore sempahore;

        public ThrottledQueueBufferedStream(int throttleCapacity)
        {
            sempahore = new Semaphore(throttleCapacity, throttleCapacity);
        }


        protected override void EnqueueChunk(byte[] chunk)
        {
            sempahore.WaitOne();
            DataChunksQueue.EnqueueAndDispatch(chunk, ChunkDequeued);
        }

        void ChunkDequeued()
        {
            sempahore.Release();
        }
    }
}