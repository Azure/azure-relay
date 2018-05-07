// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Relay;
using Microsoft.Azure.Relay.AspNetCore;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Relay.AspNetCore.Listener
{
    internal static class Utilities
    {
        internal static readonly int WriteRetryLimit = 1000;

        internal static TokenProvider CreateTokenProvider()
        {
            return AspNetCore.Utilities.CreateTokenProvider();
        }

        internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);
        // Minimum support for Windows 7 is assumed.
        internal static readonly bool IsWin8orLater;

        static Utilities()
        {
            var win8Version = new Version(6, 2);
            IsWin8orLater = (Environment.OSVersion.Version >= win8Version);
        }
        
        internal static AzureRelayListener CreateHttpServer(out string baseAddress, TokenProvider tp = null)
        {
            string root;
            return CreateDynamicHttpServer(string.Empty, out root, out baseAddress, tp != null ? tp : CreateTokenProvider());
        }

        internal static AzureRelayListener CreateDynamicHttpServer(string basePath, out string root, out string baseAddress, TokenProvider tp = null)
        {
            if ( basePath.StartsWith("/"))
            {
                basePath = basePath.Substring(1);
            }
            var rootUri = new Uri(Microsoft.Azure.Relay.AspNetCore.Utilities.GetRelayUrl());
            var prefix = AzureRelayUrlPrefix.Create(new Uri(rootUri, basePath).AbsoluteUri, tp != null ? tp : CreateTokenProvider());
            root = rootUri.ToString();
            baseAddress = prefix.FullPrefix;

            var listener = new AzureRelayListener(new AzureRelayOptions(), new LoggerFactory());
            listener.Options.UrlPrefixes.Add(prefix);
            listener.Start();
            return listener;
        }

        internal static AzureRelayListener CreateHttpServerReturnRoot(string path, out string root)
        {
            string baseAddress;
            return CreateDynamicHttpServer(path, out root, out baseAddress);
        }
        
        internal static AzureRelayListener CreateHttpsServer()
        {
            return CreateServer(Microsoft.Azure.Relay.AspNetCore.Utilities.GetRelayUrl(), string.Empty);
        }

        internal static AzureRelayListener CreateServer(string baseUrl, string path)
        {
            var listener = new AzureRelayListener(new AzureRelayOptions()
            {
                TokenProvider = Microsoft.Azure.Relay.AspNetCore.Utilities.CreateTokenProvider()
            }, new LoggerFactory());
            listener.Options.UrlPrefixes.Add(AzureRelayUrlPrefix.Create(new Uri(new Uri(baseUrl), path).AbsoluteUri));
            listener.Start();
            return listener;
        }

        /// <summary>
        /// AcceptAsync extension with timeout. This extension should be used in all tests to prevent
        /// unexpected hangs when a request does not arrive.
        /// </summary>
        internal static async Task<RequestContext> AcceptAsync(this AzureRelayListener server, TimeSpan timeout)
        {
            var acceptTask = server.AcceptAsync();
            var completedTask = await Task.WhenAny(acceptTask, Task.Delay(timeout));

            if (completedTask == acceptTask)
            {
                return await acceptTask;
            }
            else
            {
                server.Dispose();
                throw new TimeoutException("AcceptAsync has timed out.");
            }
        }
    }
}
