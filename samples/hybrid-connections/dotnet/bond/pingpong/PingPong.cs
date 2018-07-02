// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace pingpong
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Bond.Comm;
    using Bond.Examples.PingPong;
    using Microsoft.Azure.Relay;
    using Relay.Bond.Epoxy;

    public static class PingPong
    {
        static RelayEpoxyConnection pingConnection;

        public static void Main(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("pingpong [ns] [hc] [keyname] [key]");
                return;
            }

            var ns = args[0];
            var hc = args[1];
            var keyname = args[2];
            var key = args[3];

            // Create a new hybrid connection listener to listen for 
            // incoming connections.
            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(keyname, key);
            var address = $"sb://{ns}/{hc}";

            var transport = SetupAsync(address, tokenProvider).Result;

            var tasks = MakeRequestsAndPrintAsync(5);

            Task.WaitAll(tasks);

            Shutdown(transport);

            Console.WriteLine("\n\n\nDone with all requests. Press enter to exit.");
            Console.ReadLine();
        }

        static async Task<RelayEpoxyTransport> SetupAsync(string address, TokenProvider tokenProvider)
        {
            var transport = new RelayEpoxyTransportBuilder(tokenProvider)
                .SetLogSink(new ConsoleLogger())
                .Construct();

            var pingPongService = new PingPongService();
            RelayEpoxyListener pingPongListener = transport.MakeListener(address);
            pingPongListener.AddService(pingPongService);

            await pingPongListener.StartAsync();

            pingConnection = await transport.ConnectToAsync(address);

            return transport;
        }

        static void Shutdown(RelayEpoxyTransport transport)
        {
            Task.WaitAll(transport.StopAsync(), pingConnection.StopAsync());
        }

        static Task[] MakeRequestsAndPrintAsync(int numRequests)
        {
            var pingPongProxy = new PingPongProxy<RelayEpoxyConnection>(pingConnection);

            var tasks = new Task[numRequests];

            var rnd = new Random();

            foreach (var requestNum in Enumerable.Range(0, numRequests))
            {
                UInt16 delay = (UInt16) rnd.Next(2000);
                tasks[requestNum] = DoPingPong(pingPongProxy, requestNum, "ping" + requestNum, delay);
            }

            return tasks;
        }

        static async Task DoPingPong(PingPongProxy<RelayEpoxyConnection> proxy, int requestNum, string payload, UInt16 delay)
        {
            var request = new PingRequest {Payload = payload, DelayMilliseconds = delay};
            IMessage<PingResponse> message = await proxy.PingAsync(request);

            if (message.IsError)
            {
                Error error = message.Error.Deserialize();
                Console.WriteLine($"Request #{requestNum} failed: {error.error_code}: {error.message}");
            }
            else
            {
                PingResponse response = message.Payload.Deserialize();
                Console.WriteLine($"Request #{requestNum} response: \"{response.Payload}\". Delay: {delay}");
            }
        }
    }
}