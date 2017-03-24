// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridge
{
    using System;
    using System.IO;

    public class CompositeDuplexStream : Stream
    {
        readonly Stream inputStream;
        readonly Stream outputStream;

        public CompositeDuplexStream(Stream inputStream, Stream outputStream)
        {
            this.inputStream = inputStream;
            this.outputStream = outputStream;
        }

        protected CompositeDuplexStream()
        {
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
            get { return true; }
        }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }

        public override long Position
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public override void Close()
        {
            base.Close();
            inputStream.Close();
            outputStream.Close();
        }

        public override void Flush()
        {
            try
            {
                outputStream.Flush();
            }
            catch (NotSupportedException)
            {
                // ok
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return inputStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            outputStream.Write(buffer, offset, count);
        }
    }
}