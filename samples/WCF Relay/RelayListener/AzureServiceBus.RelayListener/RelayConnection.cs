//  
//  Copyright © Microsoft Corporation, All Rights Reserved
// 
//  Licensed under the Apache License, Version 2.0 (the "License"); 
//  you may not use this file except in compliance with the License. 
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0 
// 
//  THIS CODE IS PROVIDED *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
//  OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION
//  ANY IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A
//  PARTICULAR PURPOSE, MERCHANTABILITY OR NON-INFRINGEMENT.
// 
//  See the Apache License, Version 2.0 for the specific language
//  governing permissions and limitations under the License. 

namespace AzureServiceBus.RelayListener
{
    using System;
    using System.IO;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus.Web;

    public sealed class RelayConnection : Stream, IDisposable
    {
        readonly IDuplexSessionChannel channel;
        readonly SemaphoreSlim readerSemaphore = new SemaphoreSlim(1);
        Stream pendingReaderStream;

        public RelayConnection(IDuplexSessionChannel channel)
        {
            this.channel = channel;
            this.WriteTimeout = this.ReadTimeout = 60*1000; // 60s default
        }

        public override bool CanTimeout => true;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;

        public override int WriteTimeout { get; set; }
        public override int ReadTimeout { get; set; }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

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

        public override void Close()
        {
            Shutdown();
            base.Close();
        }

        public void Shutdown()
        {
            ShutdownAsync().GetAwaiter().GetResult();
        }

        public Task ShutdownAsync()
        {
            if (channel.State == CommunicationState.Opened)
            {
                var msg = Message.CreateMessage(MessageVersion.Default, "eof");
                return Task.Factory.FromAsync(channel.BeginSend, channel.EndSend, msg,
                    TimeSpan.FromMilliseconds(WriteTimeout), null);
            }
            return Task.FromResult(true);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count).GetAwaiter().GetResult();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAsync(buffer, offset, count).GetAwaiter().GetResult();
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (channel.State != CommunicationState.Opened)
            {
                throw new InvalidOperationException();
            }

            // local cancellation source dependent on outer token that nukes the channel 
            // on abort. That renders the channel unusable for further operations while a 
            // timeout expiry might not.
            using (var ct = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                ct.Token.Register(() => channel.Abort());
                // create and send message
                var msg = StreamMessageHelper.CreateMessage(MessageVersion.Default, string.Empty,
                    new MemoryStream(buffer, offset, count));
                msg.Headers.Action = null;
                return Task.Factory.FromAsync(channel.BeginSend, channel.EndSend, msg,
                    TimeSpan.FromMilliseconds(WriteTimeout), null);
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            if (channel.State != CommunicationState.Opened)
            {
                throw new InvalidOperationException();
            }
            
            await readerSemaphore.WaitAsync(cancellationToken);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (pendingReaderStream == null)
                    {
                        // local cancellation source dependent on outer token that nukes the channel 
                        // on abort. That renders the channel unusable for further operations while a 
                        // timeout expiry might not.
                        using (var ct = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                        {
                            ct.Token.Register(() => channel.Abort());
                            
                            // read from the channel
                            var msg = await Task.Factory.FromAsync(channel.BeginReceive,
                                (Func<IAsyncResult, Message>) channel.EndReceive,
                                TimeSpan.FromMilliseconds(ReadTimeout), null);
                            if (!msg.IsEmpty && msg.Headers.Action != "eof")
                            {
                                pendingReaderStream = StreamMessageHelper.GetStream(msg);
                            }
                        }
                    }
                    if (pendingReaderStream != null)
                    {
                        var bytesRead = await pendingReaderStream.ReadAsync(buffer, offset, count,
                            CancellationTokenSource.CreateLinkedTokenSource(
                                new CancellationTokenSource(TimeSpan.FromMilliseconds(ReadTimeout)).Token,
                                cancellationToken).Token);
                        if (bytesRead == 0)
                        {
                            pendingReaderStream = null;
                            continue;
                        }
                        return bytesRead;
                    }
                    return 0;
                }
                throw new OperationCanceledException(cancellationToken);
            }
            finally
            {
                readerSemaphore.Release();
            }
        }
    }

    public enum RelayAddressType
    {
        Configured,
        Dynamic
    }
}