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

namespace RelaySamples
{
    using System;
    using System.ServiceModel;
    using System.ServiceModel.Description;
    using System.ServiceModel.Dispatcher;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus;

    class Program : IDynamicListenerSample
    {
        public async Task Run(string serviceBusHostName, string listenToken)
        {
            // create the service URI based on the service namespace
            var serviceAddress = new UriBuilder("https", serviceBusHostName, -1, "echoservice/service").Uri;
            
            // create the service host reading the configuration
            var host = new ServiceHost(typeof(EchoService), serviceAddress);
            var db = host.Description.Behaviors.Find<ServiceDebugBehavior>();
            db.HttpsHelpPageUrl = new Uri(serviceAddress, "wsdl");
            // add the Service Bus credentials to all endpoints specified in configuration
            foreach (var endpoint in host.Description.Endpoints)
            {
                if (endpoint.Contract.ContractType == typeof (IMetadataExchange))
                {
                    db.HttpsHelpPageUrl = new Uri(serviceAddress, "mex");
                }
                endpoint.Behaviors.Add(new ServiceRegistrySettings(DiscoveryType.Public));
                endpoint.Behaviors.Add(
                    new TransportClientEndpointBehavior(
                        TokenProvider.CreateSharedAccessSignatureTokenProvider(listenToken)));
            }
            
            // open the service
            host.Open();

            foreach (var channelDispatcherBase in host.ChannelDispatchers)
            {
                var channelDispatcher = channelDispatcherBase as ChannelDispatcher;
                if (channelDispatcher != null)
                {
                    foreach (var endpointDispatcher in channelDispatcher.Endpoints)
                    {
                        Console.WriteLine("Listening at: {0}", endpointDispatcher.EndpointAddress);
                    }
                }
            }

            Console.WriteLine("Press [Enter] to exit");
            Console.ReadLine();

            host.Close();
        }
    }
}