// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Azure.Relay.AspNetCore;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Hosting
{
    public static class WebHostBuilderAzureRelayExtensions
    {
        /// <summary>
        /// Specify AzureRelay as the server to be used by the web host.
        /// </summary>
        /// <param name="hostBuilder">
        /// The Microsoft.AspNetCore.Hosting.IWebHostBuilder to configure.
        /// </param>
        /// <returns>
        /// The Microsoft.AspNetCore.Hosting.IWebHostBuilder.
        /// </returns>
        public static IWebHostBuilder UseAzureRelay(this IWebHostBuilder hostBuilder)
        {
            return hostBuilder.ConfigureServices(services => {
                services.AddSingleton<IServer, MessagePump>();
                services.AddAuthenticationCore();
            });
        }

        /// <summary>
        /// Specify AzureRelay as the server to be used by the web host.
        /// </summary>
        /// <param name="hostBuilder">
        /// The Microsoft.AspNetCore.Hosting.IWebHostBuilder to configure.
        /// </param>
        /// <param name="options">
        /// A callback to configure AzureRelay options.
        /// </param>
        /// <returns>
        /// The Microsoft.AspNetCore.Hosting.IWebHostBuilder.
        /// </returns>
        public static IWebHostBuilder UseAzureRelay(this IWebHostBuilder hostBuilder, Action<AzureRelayOptions> options)
        {
            return hostBuilder.UseAzureRelay().ConfigureServices(services =>
            {
                services.Configure(options);
            });
        }
    }
}
