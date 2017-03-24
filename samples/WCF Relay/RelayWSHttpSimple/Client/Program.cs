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

    class Program : IHttpSenderSample
    {
        public async Task Run(string httpAddress, string sendToken)
        {
            var channelFactory =
                new ChannelFactory<IEchoChannel>("ServiceBusEndpoint", new EndpointAddress(httpAddress));
            channelFactory.Endpoint.Behaviors.Add(
                new TransportClientEndpointBehavior(TokenProvider.CreateSharedAccessSignatureTokenProvider(sendToken)));

            using (IEchoChannel channel = channelFactory.CreateChannel())
            {
                Console.WriteLine("Enter text to echo (or [Enter] to exit):");
                string input = Console.ReadLine();
                while (input != string.Empty)
                {
                    try
                    {
                        Console.WriteLine("Server echoed: {0}", channel.Echo(input));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error: " + e.Message);
                    }
                    input = Console.ReadLine();
                }
                channel.Close();
            }
            channelFactory.Close();
        }
    }
}