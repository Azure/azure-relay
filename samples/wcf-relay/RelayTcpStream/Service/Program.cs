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

    class Program : ITcpListenerSample
    {
        public async Task Run(string listenAddress, string listenToken)
        {
            // The host for our service is a regular WCF service host. You can use 
            // all extensibility options of WCF and you can also host non-Relay
            // endpoints alongside the Relay endpoints on this host
            using (var host = new ServiceHost(typeof(StreamServer)))
            {
                // Now we're adding the service endpoint with a listen address on Service Bus
                // and using the NetTcpRelayBinding, which is a variation of the regular
                // NetTcpBinding of WCF with the difference that this one listens on the
                // Service Bus Relay service.                
                // Since the Service Bus Relay requires Authorization, we then also add the 
                // SAS token provider to the endpoint.
                var endpoint = host.AddServiceEndpoint(typeof(StreamServer),
                                                       new NetTcpRelayBinding { IsDynamic = false },
                                                        listenAddress);

                endpoint.EndpointBehaviors.Add(
                        new TransportClientEndpointBehavior(
                            TokenProvider.CreateSharedAccessSignatureTokenProvider(listenToken)));
                endpoint.EndpointBehaviors.Add(
                    new InstanceFactory<StreamServer>(() => new StreamServer(Console.OpenStandardOutput())));

                // once open returns, the service is open for business. Not async for legibility.
                host.Open();
                Console.WriteLine("Service listening at address {0}", listenAddress);
                Console.WriteLine("Press [Enter] to close the listener and exit.");
                Console.ReadLine();
            }
        }
    }
}