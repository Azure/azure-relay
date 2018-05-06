// Copyright © Microsoft Corporation, All Rights Reserved
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0 
// 
// THIS CODE IS PROVIDED ON AN *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION
// ANY IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A
// PARTICULAR PURPOSE, MERCHANTABILITY OR NON-INFRINGEMENT.
// 
// See the Apache License, Version 2.0 for the specific language
// governing permissions and limitations under the License. 

// Adapted from http://thrift.apache.org/tutorial/csharp

namespace Server
{
    using System;
    using Microsoft.Azure.Relay;
    using Thrift.Server;
    using Thrift.Transport;

    public class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("server [ns] [hc] [keyname] [key]");
                return;
            }

            var ns = args[0];
            var hc = args[1];
            var keyname = args[2];
            var key = args[3];

            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(keyname, key);
            var listenAddress = new Uri(string.Format("sb://{0}/{1}", ns, hc));
            try
            {
                CalculatorHandler handler = new CalculatorHandler();
                Calculator.Processor processor = new Calculator.Processor(handler);

                TServerTransport serverTransport = new HybridConnectionListenerServerTransport(
                    new HybridConnectionListener(listenAddress, tokenProvider));

                TServer server = new TSimpleServer(processor, serverTransport);

                // Use this for a multithreaded server
                // server = new TThreadPoolServer(processor, serverTransport);

                Console.WriteLine("Starting the server...");
                server.Serve();
            }
            catch (Exception x)
            {
                Console.WriteLine(x.StackTrace);
            }
            Console.WriteLine("done.");
        }
    }
}