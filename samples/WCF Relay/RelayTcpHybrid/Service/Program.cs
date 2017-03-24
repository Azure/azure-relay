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
    using System.ServiceModel;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus;

    // This is an all-in-one Relay service that can be hosted through the 
    // Service Bus Relay. 
    [ServiceContract(Namespace = "", Name = "echo"),
     ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    class Program : IDynamicListenerSample
    {
        public async Task Run(string serviceBusHostName, string listenToken)
        {
            var listenAddress = new UriBuilder("sb", serviceBusHostName, -1, "hybridconnect").ToString();
            using (ServiceHost host = new ServiceHost(this))
            {
                var binding = new NetTcpRelayBinding(EndToEndSecurityMode.None, RelayClientAuthenticationType.RelayAccessToken)
                {
                   ConnectionMode = TcpRelayConnectionMode.Hybrid
                };
                host.AddServiceEndpoint(
                    GetType(),
                    binding,
                    listenAddress)
                    .EndpointBehaviors.Add(
                        new TransportClientEndpointBehavior(
                            TokenProvider.CreateSharedAccessSignatureTokenProvider(listenToken)));

                host.Open();
                Console.WriteLine("Service listening at address {0}", listenAddress);
                Console.WriteLine("Press [Enter] to close the listener and exit.");
                Console.ReadLine();
                host.Close();
            }
        }

        [OperationContract]
        async Task<string> Echo(string input)
        {
            Console.WriteLine("\tCall received with input \"{0}\"", input);
            return input;
        }
    }
}