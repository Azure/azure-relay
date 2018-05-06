//   
//   Copyright © Microsoft Corporation, All Rights Reserved
// 
//   Licensed under the Apache License, Version 2.0 (the "License"); 
//   you may not use this file except in compliance with the License. 
//   You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0 
// 
//   THIS CODE IS PROVIDED *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
//   OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION
//   ANY IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A
//   PARTICULAR PURPOSE, MERCHANTABILITY OR NON-INFRINGEMENT.
// 
//   See the Apache License, Version 2.0 for the specific language
//   governing permissions and limitations under the License. 

namespace RelaySamples
{
    using System;
    using System.IO;
    using System.ServiceModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus;

    class StreamClient : Stream
    {
        readonly ChannelFactory<IClient> channelFactory;
        readonly object channelMutex = new object();
        IClient channel;

        public StreamClient(string sendAddress, string sendToken, bool isDynamicEndpoint)
        {
            // first we create the WCF "channel factory" that will be used to create a proxy
            // to the remote service in the next step. The channel factory is constructed
            // using the NetTcpRelayBinding, which comes with the Service Bus client assembly
            // and understands the Service Bus Authorization model for clients. 
            this.channelFactory = new ChannelFactory<IClient>(
                new NetTcpRelayBinding {IsDynamic = isDynamicEndpoint }, 
                sendAddress);

            // to configure authorization with Service Bus, we now add the SAS token provider 
            // into which we pass the externally issued SAS token via a WCF behavior that is also 
            // included in the Service Bus client
            this.channelFactory.Endpoint.EndpointBehaviors.Add(
                new TransportClientEndpointBehavior(
                    TokenProvider.CreateSharedAccessSignatureTokenProvider(sendToken)));
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            lock (this.channelMutex)
            {
                if (this.channel == null)
                {
                    this.channel = this.channelFactory.CreateChannel();
                }
            }
            if (offset == 0 && buffer.Length == count)
            {
                await this.channel.WriteAsync(buffer);
            }
            else
            {
                var writeBuffer = new byte[count];
                Array.ConstrainedCopy(buffer, 0, writeBuffer, 0, count);
                await this.channel.WriteAsync(writeBuffer);
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            lock (this.channelMutex)
            {
                if (this.channel == null)
                {
                    this.channel = this.channelFactory.CreateChannel();
                }
            }

            var result = await this.channel.ReadAsync(count);
            if (result == null || result.Length > count)
            {
                throw new InvalidDataException();
            }
            Array.ConstrainedCopy(result, 0, buffer, offset, result.Length);
            return result.Length;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.ReadAsync(buffer, offset, count).GetAwaiter().GetResult();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.WriteAsync(buffer, offset, count).GetAwaiter().GetResult();
        }

        // this is the service contract that echoes the contract of the service
        [ServiceContract(Namespace = "", Name = "txf", SessionMode = SessionMode.Required)]
        interface IClient : IClientChannel
        {
            [OperationContract]
            Task WriteAsync(byte[] data);

            [OperationContract]
            Task<byte[]> ReadAsync(int max);
        }
    }
}