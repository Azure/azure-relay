// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.Relay.ReverseProxy
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Azure Relay Http Reverse Proxy\n(c) Microsoft Corporation\n");
            if (args == null || args.Length != 2)
            {
                Console.WriteLine("Requires two arguments: connection string and target uri.");
                Console.WriteLine("Example:");
                Console.WriteLine($"\tdotnet.exe {Assembly.GetEntryAssembly().ManifestModule.Name} Endpoint=sb://contoso.servicebus.windows.net/;SharedAccessKeyName=ListenKey;SharedAccessKey=XXXX,EntityPath=your_hc_name http://host:80/api/");
                return;
            }

            string connectionString = args[0];
            Uri targetUri = new Uri(args[1].EnsureEndsWith("/"));
            RunAsync(connectionString, targetUri).GetAwaiter().GetResult();
        }

        static async Task RunAsync(string connectionString, Uri targetUri)
        {
            var hybridProxy = new HybridConnectionReverseProxy(connectionString, targetUri);
            await hybridProxy.OpenAsync(CancellationToken.None);

            Console.ReadLine();

            await hybridProxy.CloseAsync(CancellationToken.None);
        }
    }
}
