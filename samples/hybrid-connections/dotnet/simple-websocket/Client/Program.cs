// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.Relay;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Client
{
    class Program
    {
        static async Task RunAsync(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("client [ns] [hc] [keyname] [key]");
                return;
            }

            Console.WriteLine("Enter lines of text to send to the server with ENTER");

            var ns = args[0];
            var hc = args[1];
            var keyname = args[2];
            var key = args[3];

            // Create a new hybrid connection client
            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(keyname, key);
            var client = new HybridConnectionClient(new Uri(String.Format("sb://{0}/{1}", ns, hc)),tokenProvider);

            // Initiate the connection
            var relayConnection = await client.CreateConnectionAsync();

            // We run two conucrrent loops on the connection. One 
            // reads input from the console and writes it to the connection 
            // with a stream writer. The other reads lines of input from the 
            // connection with a stream reader and writes them to the console. 
            // Entering a blank line will shut down the write task after 
            // sending it to the server. The server will then cleanly shut down
            // the connection will will terminate the read task.
            
            var reads = Task.Run(async () => {
                // initialize the stream reader over the connection
                var reader = new StreamReader(relayConnection);
                var writer = Console.Out;
                do
                {
                    // read a full line of UTF-8 text up to newline
                    string line = await reader.ReadLineAsync();
                    // if the string is empty or null, we are done.
                    if (String.IsNullOrEmpty(line))
                        break;
                    // write to the console
                    await writer.WriteLineAsync(line);
                }
                while (true);
            });

            // read from the console and write to the hybrid connection
            var writes = Task.Run(async () => {
                var reader = Console.In;
                var writer = new StreamWriter(relayConnection) { AutoFlush = true };
                do
                {
                    // read a line form the console
                    string line = await reader.ReadLineAsync();
                    // write the line out, also when it's empty
                    await writer.WriteLineAsync(line);
                    // quit when the line was empty
                    if (String.IsNullOrEmpty(line))
                        break;
                }
                while (true);
            });

            // wait for both tasks to complete
            await Task.WhenAll(reads, writes);
            await relayConnection.CloseAsync(CancellationToken.None);
        }

        static void Main(string[] args)
        {
            RunAsync(args).GetAwaiter().GetResult();
        }
    }
}
