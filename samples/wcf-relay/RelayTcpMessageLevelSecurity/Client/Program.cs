
namespace RelaySamples
{
    using System;
    using System.ServiceModel;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus;

    class Program : ITcpSenderSample
    {
        public async Task Run(string sendAddress, string sendToken)
        {
            var channelFactory =
                new ChannelFactory<IEchoChannel>("RelayEndpoint",
                    new EndpointAddress(new Uri(sendAddress), EndpointIdentity.CreateDnsIdentity("localhost")))
                {
                    Credentials =
                    {
                        UserName =
                        {
                            UserName = "test1",
                            Password = "1tset"
                        }
                    }
                };
            channelFactory.Endpoint.Behaviors.Add(
                new TransportClientEndpointBehavior(TokenProvider.CreateSharedAccessSignatureTokenProvider(sendToken)));

            var channel = channelFactory.CreateChannel();
            try
            {
                channel.Open();


                Console.Write("Enter the text to echo (or press [Enter] to exit): ");
                var input = Console.ReadLine();
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
            finally
            {
                channel.Abort();
            }
            channelFactory.Close();
        }
    }
}