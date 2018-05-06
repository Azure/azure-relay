// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridge
{
    using System;
    using System.IO;
    using System.Threading;

    public class QueueBufferedStream : Stream
    {
        readonly ManualResetEvent done;
        readonly TimeSpan naglingDelay;
        byte[] currentChunk;
        int currentChunkPosition;
        volatile bool isStreamAtEnd;

        public QueueBufferedStream()
            : this(TimeSpan.Zero)
        {
        }

        public QueueBufferedStream(TimeSpan naglingDelay)
        {
            this.naglingDelay = naglingDelay;
            done = new ManualResetEvent(false);
            DataChunksQueue = new InputQueue<byte[]>();
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return !isStreamAtEnd; }
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        protected InputQueue<byte[]> DataChunksQueue { get; }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }
            if (offset < 0 || offset >= buffer.Length)
            {
                throw new ArgumentOutOfRangeException("offset");
            }
            if (count < 0 || offset + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            if (count == 0)
            {
                return 0;
            }

            bool waitForChunk = true;
            int bytesRead = 0;

            while (true)
            {
                if (currentChunk == null)
                {
                    if (isStreamAtEnd)
                    {
                        return 0;
                    }
                    if (waitForChunk)
                    {
                        IAsyncResult dequeueAsyncResult = DataChunksQueue.BeginDequeue(TimeSpan.MaxValue, null, null);
                        if (!dequeueAsyncResult.CompletedSynchronously &&
                            WaitHandle.WaitAny(new[] {dequeueAsyncResult.AsyncWaitHandle, done}) == 1)
                        {
                            return 0;
                        }
                        currentChunk = DataChunksQueue.EndDequeue(dequeueAsyncResult);
                        waitForChunk = false;
                    }
                    else
                    {
                        if (!DataChunksQueue.Dequeue(naglingDelay, out currentChunk))
                        {
                            return bytesRead;
                        }
                    }
                    currentChunkPosition = 0;
                }
                else
                {
                    waitForChunk = false;
                }

                int bytesAvailable = currentChunk.Length - currentChunkPosition;
                int bytesToCopy;
                if (bytesAvailable > count)
                {
                    bytesToCopy = count;
                    Buffer.BlockCopy(
                        currentChunk,
                        currentChunkPosition,
                        buffer,
                        offset,
                        count);
                    currentChunkPosition += count;
                    return bytesRead + bytesToCopy;
                }
                bytesToCopy = bytesAvailable;
                Buffer.BlockCopy(
                    currentChunk,
                    currentChunkPosition,
                    buffer,
                    offset,
                    bytesToCopy);
                currentChunk = null;
                currentChunkPosition = 0;
                bytesRead += bytesToCopy;
                offset += bytesToCopy;
                count -= bytesToCopy;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }
            if (offset < 0 || offset >= buffer.Length)
            {
                throw new ArgumentOutOfRangeException("offset");
            }
            if (count < 0 || offset + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            if (count == 0)
            {
                return;
            }

            byte[] chunk = new byte[count];
            Buffer.BlockCopy(buffer, offset, chunk, 0, count);

            if (isStreamAtEnd)
            {
                throw new InvalidOperationException("EOF");
            }
            EnqueueChunk(chunk);
        }

        protected virtual void EnqueueChunk(byte[] chunk)
        {
            DataChunksQueue.EnqueueAndDispatch(chunk);
        }

        public void SetEndOfStream()
        {
            isStreamAtEnd = true;
            done.Set();
        }

        public override void Close()
        {
            SetEndOfStream();
            DataChunksQueue.Close();
            base.Close();
        }
    }
}