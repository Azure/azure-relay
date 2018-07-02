// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace notifyevent
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Bond.Comm.Epoxy;
    using Bond.Examples.NotifyEvent;
    using Microsoft.Azure.Relay;
    using Relay.Bond.Epoxy;

    public static class NotifyEvent
    {
        static RelayEpoxyConnection s_connection;

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

            MakeRequestsAndPrint(5);

            Console.WriteLine("Done with all requests.");

            // TODO: Shutdown not yet implemented.
            // transport.StopAsync().Wait();

            Console.ReadLine();
        }

        static async Task<RelayEpoxyTransport> SetupAsync(string address, TokenProvider tokenProvider)
        {
            var transport = new RelayEpoxyTransportBuilder(tokenProvider)
                .SetLogSink(new ConsoleLogger())
                .Construct();

            var assignAPortEndPoint = new IPEndPoint(IPAddress.Loopback, EpoxyTransport.DefaultInsecurePort);

            var notifyService = new NotifyEventService();
            RelayEpoxyListener notifyListener = transport.MakeListener(address);
            notifyListener.AddService(notifyService);

            await notifyListener.StartAsync();

            s_connection = await transport.ConnectToAsync(address, CancellationToken.None);

            return transport;
        }

        static void MakeRequestsAndPrint(int numRequests)
        {
            var notifyEventProxy = new NotifyEventProxy<RelayEpoxyConnection>(s_connection);

            var rnd = new Random();

            foreach (var requestNum in Enumerable.Range(0, numRequests))
            {
                UInt16 delay = (UInt16) rnd.Next(2000);
                DoNotify(notifyEventProxy, requestNum, "notify" + requestNum, delay);
            }
        }

        static void DoNotify(NotifyEventProxy<RelayEpoxyConnection> proxy, int requestNum, string payload, UInt16 delay)
        {
            var request = new PingRequest {Payload = payload, DelayMilliseconds = delay};
            proxy.NotifyAsync(request);

            Console.WriteLine($"P Event #{requestNum} Delay: {delay}");
        }
    }
}