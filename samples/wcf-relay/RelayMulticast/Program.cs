//---------------------------------------------------------------------------------
// Microsoft (R)  Windows Azure SDK
// Software Development Kit
// 
// Copyright (c) Microsoft Corporation. All rights reserved.  
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE. 
//---------------------------------------------------------------------------------

namespace RelaySamples
{
    using System;
    using System.ServiceModel;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus;

    [ServiceContract]
    interface IChat
    {
        [OperationContract(IsOneWay = true)]
        void Hello(string nickname);
        [OperationContract(IsOneWay = true)]
        void Chat(string nickname, string text);
        [OperationContract(IsOneWay = true)]
        void Bye(string nickname);
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    class Program : IDynamicSample, IChat
    {
        public void Hello(string nickname)
        {
            Console.WriteLine("[" + nickname + "] joins");
        }

        public void Chat(string nickname, string text)
        {
            Console.WriteLine("[" + nickname + "] says: " + text);
        }

        public void Bye(string nickname)
        {
            Console.WriteLine("[" + nickname + "] leaves");
        }

        void RunChat(IChat channel, string chatNickname)
        {
            Console.WriteLine("\nPress [Enter] to exit\n");
            channel.Hello(chatNickname);

            string input = Console.ReadLine();
            while (input != string.Empty)
            {
                channel.Chat(chatNickname, input);
                input = Console.ReadLine();
            }
            channel.Bye(chatNickname);
        }

        public Task Run(string serviceBusHostName, string token)
        {
            Console.Write("Enter your nick:");
            var chatNickname = Console.ReadLine();
            Console.Write("Enter room name:");
            var session = Console.ReadLine();

            var serviceAddress = new UriBuilder("sb", serviceBusHostName, -1, "/chat/" + session).ToString();
            var netEventRelayBinding = new NetEventRelayBinding();
            var tokenBehavior = new TransportClientEndpointBehavior(TokenProvider.CreateSharedAccessSignatureTokenProvider(token));

            using (var host = new ServiceHost(this))
            {
                host.AddServiceEndpoint(typeof(IChat), netEventRelayBinding, serviceAddress)
                    .EndpointBehaviors.Add(tokenBehavior);
                host.Open();

                using (var channelFactory = new ChannelFactory<IChat>(netEventRelayBinding, serviceAddress))
                {
                    channelFactory.Endpoint.Behaviors.Add(tokenBehavior);
                    var channel = channelFactory.CreateChannel();

                    this.RunChat(channel, chatNickname);

                    channelFactory.Close();
                }
                host.Close();
            }
            return Task.FromResult(true);
        }
    }
}
