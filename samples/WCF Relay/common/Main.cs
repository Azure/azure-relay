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
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus;

    // IF YOU ARE JUST GETTING STARTED, 
    // THESE ARE NOT THE DROIDS YOU ARE LOOKING FOR
    // PLEASE REVIEW "Program.cs" IN THE SAMPLE PROJECT

    // This is a common entry point class for all samples that provides
    // the Main() method entry point called by the CLR. It loads the properties
    // stored in the "azure-relay-samples.properties" file from the user profile
    // and then allows override of the settings from environment variables.
    class AppEntryPoint
    {
        static readonly string servicebusNamespace = "SERVICEBUS_NAMESPACE";
        static readonly string servicebusEntityPath = "SERVICEBUS_ENTITY_PATH";
        static readonly string servicebusFqdnSuffix = "SERVICEBUS_FQDN_SUFFIX";
        static readonly string servicebusSendKey = "SERVICEBUS_SEND_KEY";
        static readonly string servicebusListenKey = "SERVICEBUS_LISTEN_KEY";
        static readonly string servicebusManageKey = "SERVICEBUS_MANAGE_KEY";
        static readonly string samplePropertiesFileName = "azure-relay-config.properties";
#if STA
        [STAThread]
#endif

        static void Main(string[] args)
        {
            Run();
        }

        [DebuggerStepThrough]
        static void Run()
        {
            var properties = new Dictionary<string, string>
            {
                {servicebusNamespace, null},
                {servicebusEntityPath, null},
                {servicebusFqdnSuffix, null},
                {servicebusSendKey, null},
                {servicebusListenKey, null},
                {servicebusManageKey, null}
            };

            // read the settings file created by the ./setup.ps1 file
            var settingsFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                samplePropertiesFileName);
            if (File.Exists(settingsFile))
            {
                using (var fs = new StreamReader(settingsFile))
                {
                    while (!fs.EndOfStream)
                    {
                        var readLine = fs.ReadLine();
                        if (readLine != null)
                        {
                            var propl = readLine.Trim();
                            var cmt = propl.IndexOf('#');
                            if (cmt > -1)
                            {
                                propl = propl.Substring(0, cmt).Trim();
                            }
                            if (propl.Length > 0)
                            {
                                var propi = propl.IndexOf('=');
                                if (propi == -1)
                                {
                                    continue;
                                }
                                var propKey = propl.Substring(0, propi - 1).Trim();
                                var propVal = propl.Substring(propi + 1).Trim();
                                if (properties.ContainsKey(propKey))
                                {
                                    properties[propKey] = propVal;
                                }
                            }
                        }
                    }
                }
            }

            // get overrides from the environment
            foreach (var prop in properties)
            {
                var env = Environment.GetEnvironmentVariable(prop.Key);
                if (env != null)
                {
                    properties[prop.Key] = env;
                }
            }

            var hostName = properties[servicebusNamespace] + "." + properties[servicebusFqdnSuffix];
            var rootUri = new UriBuilder("http", hostName, -1, "/").ToString();
            var netTcpUri = new UriBuilder("sb", hostName, -1, properties[servicebusEntityPath] + "/NetTcp").ToString();
            var httpUri = new UriBuilder("https", hostName, -1, properties[servicebusEntityPath] + "/Http").ToString();

            var program = Activator.CreateInstance(typeof (Program));
            if (program is ITcpListenerSampleUsingKeys)
            {
                ((ITcpListenerSampleUsingKeys) program).Run(
                    netTcpUri,
                    "samplelisten",
                    properties[servicebusListenKey])
                    .GetAwaiter()
                    .GetResult();
            }
            else if (program is ITcpSenderSampleUsingKeys)
            {
                ((ITcpSenderSampleUsingKeys) program).Run(netTcpUri, "samplesend", properties[servicebusSendKey])
                    .GetAwaiter()
                    .GetResult();
            }
            if (program is IHttpListenerSampleUsingKeys)
            {
                ((IHttpListenerSampleUsingKeys) program).Run(
                    httpUri,
                    "samplelisten",
                    properties[servicebusListenKey])
                    .GetAwaiter()
                    .GetResult();
            }
            else if (program is IHttpSenderSampleUsingKeys)
            {
                ((IHttpSenderSampleUsingKeys) program).Run(httpUri, "samplesend", properties[servicebusSendKey])
                    .GetAwaiter()
                    .GetResult();
            }

            if (program is ITcpListenerSample)
            {
                var token =
                    TokenProvider.CreateSharedAccessSignatureTokenProvider(
                        "samplelisten",
                        properties[servicebusListenKey])
                        .GetWebTokenAsync(netTcpUri, string.Empty, true, TimeSpan.FromHours(1)).GetAwaiter().GetResult();
                ((ITcpListenerSample) program).Run(netTcpUri, token).GetAwaiter().GetResult();
            }
            else if (program is ITcpSenderSample)
            {
                var token =
                    TokenProvider.CreateSharedAccessSignatureTokenProvider(
                        "samplesend",
                        properties[servicebusSendKey])
                        .GetWebTokenAsync(netTcpUri, string.Empty, true, TimeSpan.FromHours(1)).GetAwaiter().GetResult();
                ((ITcpSenderSample) program).Run(netTcpUri, token).GetAwaiter().GetResult();
            }
            if (program is IHttpListenerSample)
            {
                var token =
                    TokenProvider.CreateSharedAccessSignatureTokenProvider(
                        "samplelisten",
                        properties[servicebusListenKey])
                        .GetWebTokenAsync(httpUri, string.Empty, true, TimeSpan.FromHours(1)).GetAwaiter().GetResult();
                ((IHttpListenerSample)program).Run(httpUri, token).GetAwaiter().GetResult();
            }
            else if (program is IHttpSenderSample)
            {
                var token =
                    TokenProvider.CreateSharedAccessSignatureTokenProvider(
                        "samplesend",
                        properties[servicebusSendKey])
                        .GetWebTokenAsync(httpUri, string.Empty, true, TimeSpan.FromHours(1)).GetAwaiter().GetResult();
                ((IHttpSenderSample)program).Run(httpUri, token).GetAwaiter().GetResult();
            }
            else if (program is IDynamicSenderSample)
            {
                var token =
                    TokenProvider.CreateSharedAccessSignatureTokenProvider(
                        "rootsamplesend",
                        properties[servicebusSendKey])
                        .GetWebTokenAsync(rootUri, string.Empty, true, TimeSpan.FromHours(1)).GetAwaiter().GetResult();
                ((IDynamicSenderSample)program).Run(hostName, token).GetAwaiter().GetResult();
            }
            else if (program is IDynamicListenerSample)
            {
                var token =
                    TokenProvider.CreateSharedAccessSignatureTokenProvider(
                        "rootsamplelisten",
                        properties[servicebusListenKey])
                        .GetWebTokenAsync(rootUri, string.Empty, true, TimeSpan.FromHours(1)).GetAwaiter().GetResult();
                ((IDynamicListenerSample)program).Run(hostName, token).GetAwaiter().GetResult();
            }
            else if (program is IDynamicSample)
            {
                var token =
                    TokenProvider.CreateSharedAccessSignatureTokenProvider(
                        "rootsamplemanage",
                        properties[servicebusManageKey])
                        .GetWebTokenAsync(rootUri, string.Empty, true, TimeSpan.FromHours(1)).GetAwaiter().GetResult();
                ((IDynamicSample)program).Run(hostName, token).GetAwaiter().GetResult();
            }
            else if (program is IConnectionStringSample)
            {
                var connectionString =
                    ServiceBusConnectionStringBuilder.CreateUsingSharedAccessKey(
                        new Uri(rootUri), "rootsamplemanage",
                        properties[servicebusManageKey]);

                ((IConnectionStringSample)program).Run(connectionString).GetAwaiter().GetResult();
            }
        }
    }

    interface ITcpListenerSampleUsingKeys
    {
        Task Run(string listenAddress, string listenKeyName, string listenKeyValue);
    }

    interface IHttpListenerSampleUsingKeys
    {
        Task Run(string listenAddress, string listenKeyName, string listenKeyValue);
    }

    interface ITcpSenderSampleUsingKeys
    {
        Task Run(string sendAddress, string sendKeyName, string sendKeyValue);
    }

    interface IHttpSenderSampleUsingKeys
    {
        Task Run(string sendAddress, string sendKeyName, string sendKeyValue);
    }

    interface ITcpSenderSample
    {
        Task Run(string sendAddress, string sendToken);
    }

    interface IHttpSenderSample
    {
        Task Run(string sendAddress, string sendToken);
    }

    interface ITcpListenerSample
    {
        Task Run(string listenAddress, string listenToken);
    }

    interface IHttpListenerSample
    {
        Task Run(string listenAddress, string listenToken);
    }

    interface IDynamicSenderSample
    {
        Task Run(string serviceBusHostName, string sendToken);
    }

    interface IDynamicListenerSample
    {
        Task Run(string serviceBusHostName, string listenToken);
    }

    interface IDynamicSample
    {
        Task Run(string serviceBusHostName, string token);
    }

    interface IConnectionStringSample
    {
        Task Run(string connectionString);
    }
}