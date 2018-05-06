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
    using System.Reflection;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus;

    public sealed class RelayClient
    {
        readonly string address;
        readonly TokenProvider tokenProvider;

        public RelayClient(string address, TokenProvider tokenProvider)
        {
            this.address = address;
            this.tokenProvider = tokenProvider;
        }
        
        public async Task<RelayConnection> ConnectAsync()
        {
            var tb = new TransportClientEndpointBehavior(tokenProvider);
            var bindingElement = new TcpRelayTransportBindingElement(
                RelayClientAuthenticationType.RelayAccessToken)
            {
                TransferMode = TransferMode.Buffered,
                ConnectionMode = TcpRelayConnectionMode.Relayed,
                ManualAddressing = true
            };
            bindingElement.GetType()
                .GetProperty("TransportProtectionEnabled",
                    BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(bindingElement, true);

            var rt = new CustomBinding(
                new BinaryMessageEncodingBindingElement(),
                bindingElement);

            var cf = rt.BuildChannelFactory<IDuplexSessionChannel>(tb);
            await Task.Factory.FromAsync(cf.BeginOpen, cf.EndOpen, null);
            var ch = cf.CreateChannel(new EndpointAddress(address));
            await Task.Factory.FromAsync(ch.BeginOpen, ch.EndOpen, null);
            return new RelayConnection(ch)
            {
                WriteTimeout = (int) rt.SendTimeout.TotalMilliseconds,
                ReadTimeout = (int) rt.ReceiveTimeout.TotalMilliseconds
            };
        }

        public RelayConnection Connect()
        {
            return ConnectAsync().GetAwaiter().GetResult();
        }
    }
}