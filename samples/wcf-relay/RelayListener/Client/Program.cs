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
    using System.Threading.Tasks;
    using AzureServiceBus.RelayListener;
    using Microsoft.ServiceBus;

    class Program : ITcpSenderSample
    {
        static readonly Random rnd = new Random();

        public async Task Run(string sendAddress, string sendToken)
        {
            Console.WriteLine("Starting client on {0}", sendAddress);

            var client = new RelayClient(sendAddress, TokenProvider.CreateSharedAccessSignatureTokenProvider(sendToken));
            var connection = await client.ConnectAsync();

            await SendAndReceive(connection);

            connection.Close();
            
            Console.WriteLine("Done. ENTER to exit");
            Console.ReadLine();
        }

        Task SendAndReceive(RelayConnection connection)
        {
            // implements a simple protocol that uploads and downloads 1MB of data
            // since the connection is duplex, this happens in parallel.

            Console.WriteLine("Processing connection");
            // download
            var download = Task.Run(async () =>
            {
                Console.WriteLine("Downloading 1MByte");
                var buf = new byte[1024*1024];
                int bytesRead;
                do
                {
                    bytesRead = await connection.ReadAsync(buf, 0, buf.Length);
                } while (bytesRead > 0);
                Console.WriteLine("Download done");
            });

            // upload
            var upload = Task.Run(async () =>
            {
                Console.WriteLine("Uploading 1MByte");
                var buf = new byte[16*1024];
                for (var i = 0; i < 1024/16; i++)
                {
                    rnd.NextBytes(buf);
                    await connection.WriteAsync(buf, 0, buf.Length);
                }
                await connection.ShutdownAsync();
                Console.WriteLine("Upload done");
            });
            return Task.WhenAll(upload, download);
        }
        
        
    }
}