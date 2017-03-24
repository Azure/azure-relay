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
    using System.Threading;
    using System.Threading.Tasks;
    using AzureServiceBus.RelayListener;
    using Microsoft.ServiceBus;

    class Program : ITcpListenerSample
    {
        static readonly Random Rnd = new Random();
       
        // this semaphore is gating the number of concurrent connections this 
        // program is willing the handle. Set to 5 as an example. Once the 
        // program has more than 5 connections going, it will not accept 
        // further connections until an ongoing one completed processing
        readonly SemaphoreSlim maxConcurrentConnections = new SemaphoreSlim(5);

        public async Task Run(string listenAddress, string listenToken)
        {
            Console.WriteLine("Starting listener on {0}", listenAddress);
            using (var relayListener = new RelayListener(listenAddress, TokenProvider.CreateSharedAccessSignatureTokenProvider(listenToken), RelayAddressType.Configured))
            {
                await relayListener.StartAsync();
                RelayConnection connection;
                do
                {
                    await maxConcurrentConnections.WaitAsync();
                    try
                    {
                        connection = await relayListener.AcceptConnectionAsync(TimeSpan.MaxValue);
                    }
                    catch
                    {
                        maxConcurrentConnections.Release();
                        throw;
                    }

                    if (connection != null)
                    {
                        // not awaiting, we're handling these in parallel on the I/O thread pool
                        // and simply running into the first await inside ProcessConnection
                        // is sufficient to get the handling off this thread
#pragma warning disable 4014
                        ProcessConnection(connection).ContinueWith(
                            t => maxConcurrentConnections.Release());
#pragma warning restore 4014
                    }
                    else
                    {
                        maxConcurrentConnections.Release();
                    }

                } while (connection != null);
            }
        }

        async Task ProcessConnection(RelayConnection connection)
        {
            // implements a simple protocol that uploads and downloads 1MB of data
            // since the connection is duplex, this happens in parallel.

            Console.WriteLine("Processing connection");
            // download
            var download = Task.Run(async () =>
            {
                Console.WriteLine("downloading data");
                var buf = new byte[1024*1024];
                int totalBytes = 0, bytesRead;
                do
                {
                    totalBytes += bytesRead = await connection.ReadAsync(buf, 0, buf.Length);
                }
                while (bytesRead > 0);
                Console.WriteLine("downloaded complete, {0} bytes", totalBytes);
            });

            // upload
            var upload = Task.Run(async () =>
            {
                var buf = new byte[1024];
                for (var i = 0; i < 1024; i++)
                {
                    Rnd.NextBytes(buf);
                    await connection.WriteAsync(buf, 0, buf.Length);
                }
                await connection.ShutdownAsync();
            });
            Task.WaitAll(upload, download);
            connection.Close();
            Console.WriteLine("Connection done");
        }
    }
}