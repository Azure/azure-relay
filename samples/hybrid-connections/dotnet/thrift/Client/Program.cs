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

namespace Client
{
    using System;
    using Microsoft.Azure.Relay;
    using Thrift;
    using Thrift.Protocol;
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
            var sendAddress = new Uri($"sb://{ns}/{hc}");
            var relayClient = new HybridConnectionClient(sendAddress, tokenProvider);
            try
            {
                var relayConnection = relayClient.CreateConnectionAsync().GetAwaiter().GetResult();

                TTransport transport = new TStreamTransport(relayConnection, relayConnection);
                TProtocol protocol = new TBinaryProtocol(transport);
                Calculator.Client client = new Calculator.Client(protocol);

                transport.Open();
                try
                {
                    client.ping();
                    Console.WriteLine("ping()");

                    int sum = client.add(1, 1);
                    Console.WriteLine("1+1={0}", sum);

                    Work work = new Work();

                    work.Op = Operation.DIVIDE;
                    work.Num1 = 1;
                    work.Num2 = 0;
                    try
                    {
                        int quotient = client.calculate(1, work);
                        Console.WriteLine("Whoa we can divide by 0");
                    }
                    catch (InvalidOperation io)
                    {
                        Console.WriteLine("Invalid operation: " + io.Why);
                    }

                    work.Op = Operation.SUBTRACT;
                    work.Num1 = 15;
                    work.Num2 = 10;
                    try
                    {
                        int diff = client.calculate(1, work);
                        Console.WriteLine("15-10={0}", diff);
                    }
                    catch (InvalidOperation io)
                    {
                        Console.WriteLine("Invalid operation: " + io.Why);
                    }

                    SharedStruct log = client.getStruct(1);
                    Console.WriteLine("Check log: {0}", log.Value);
                }
                finally
                {
                    transport.Close();
                }
            }
            catch (TApplicationException x)
            {
                Console.WriteLine(x.StackTrace);
            }
        }
    }
}