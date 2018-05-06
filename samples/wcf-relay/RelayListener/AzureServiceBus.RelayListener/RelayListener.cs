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
    using System.Reflection;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus;

    public sealed class RelayListener : IDisposable
    {
        readonly string address;
        readonly RelayAddressType relayAddressType;
        readonly TokenProvider tokenProvider;
        object listenerMutex = new object();
        IChannelListener<IDuplexSessionChannel> listener;
        CustomBinding listenerBinding;
        static readonly string ExceptionMessageListenerHasNotBeenStarted = "Listener has not been started";
        static readonly string ExceptionMessageListenerHasAlreadyBeenStarted = "Listener has not been started";

        public RelayListener(string address, TokenProvider tokenProvider, RelayAddressType relayAddressType)
        {
            this.address = address;
            this.relayAddressType = relayAddressType;
            this.tokenProvider = tokenProvider;
        }
        
        public async Task StartAsync()
        {
            if (listener != null)
            {
                throw new InvalidOperationException(ExceptionMessageListenerHasAlreadyBeenStarted);
            }

            try
            {
                var tcpRelayTransportBindingElement =
                    new TcpRelayTransportBindingElement(RelayClientAuthenticationType.RelayAccessToken)
                    {
                        TransferMode = TransferMode.Buffered,
                        ConnectionMode = TcpRelayConnectionMode.Relayed,
                        IsDynamic = (relayAddressType == RelayAddressType.Dynamic),
                        ManualAddressing = true
                    };
                tcpRelayTransportBindingElement.GetType()
                    .GetProperty("TransportProtectionEnabled",
                        BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(tcpRelayTransportBindingElement, true);

                var tb = new TransportClientEndpointBehavior(tokenProvider);
                this.listenerBinding = new CustomBinding(
                    new BinaryMessageEncodingBindingElement(),
                    tcpRelayTransportBindingElement);
                
                listener = listenerBinding.BuildChannelListener<IDuplexSessionChannel>(new Uri(address), tb);
                await Task.Factory.FromAsync(listener.BeginOpen, listener.EndOpen, null);
            }
            catch
            {
                listener = null;
                throw;
            }
        }

        public void Stop()
        {
            if (listener == null)
            {
                throw new InvalidOperationException(ExceptionMessageListenerHasNotBeenStarted);
            }

            listener.Close();
            listener = null;
        }

        public async Task<RelayConnection> AcceptConnectionAsync(TimeSpan timeout)
        {
            if (listener == null)
            {
                throw new InvalidOperationException(ExceptionMessageListenerHasNotBeenStarted);
            }
            var duplexSessionChannel = await Task.Factory.FromAsync(listener.BeginAcceptChannel,
                (Func<IAsyncResult, IDuplexSessionChannel>)listener.EndAcceptChannel,
                timeout, null);
            await Task.Factory.FromAsync(duplexSessionChannel.BeginOpen, duplexSessionChannel.EndOpen, null);
            return new RelayConnection(duplexSessionChannel)
            {
                WriteTimeout = (int)listenerBinding.SendTimeout.TotalMilliseconds,
                ReadTimeout = (int)listenerBinding.ReceiveTimeout.TotalMilliseconds
            };
        }

        public RelayConnection AcceptConnection(TimeSpan timeout)
        {
            return AcceptConnectionAsync(timeout).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            if (listener != null)
            {
                this.Stop();
            }
        }
    }
}