// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridgeServerAgent
{
    using System;
    using System.Configuration;
    using System.ServiceProcess;
    using PortBridge;

    class Program
    {
        static bool runOnConsole;
        static string serviceNamespace;
        static string accessRuleName;
        static string accessRuleKey;
        static string permittedPorts;
        static string localHostName = Environment.MachineName;

        static void Main(string[] args)
        {
            PrintLogo();

            var settings = ConfigurationManager.GetSection("portBridge") as PortBridgeSection;
            if (settings != null)
            {
                serviceNamespace = settings.ServiceNamespace;
                accessRuleName = settings.AccessRuleName;
                accessRuleKey = settings.AccessRuleKey;
                if (!string.IsNullOrEmpty(settings.LocalHostName))
                {
                    localHostName = settings.LocalHostName;
                }
            }

            if (!ParseCommandLine(args))
            {
                PrintUsage();
                return;
            }

            PortBridgeServiceForwarderHost host = new PortBridgeServiceForwarderHost();
            if (settings != null && settings.HostMappings.Count > 0)
            {
                foreach (HostMappingElement hostMapping in settings.HostMappings)
                {
                    string targetHostAlias = hostMapping.TargetHost;
                    if (string.Equals(targetHostAlias, "localhost", StringComparison.OrdinalIgnoreCase))
                    {
                        targetHostAlias = localHostName;
                    }
                    host.Forwarders.Add(
                        new ServiceConnectionForwarder(
                            serviceNamespace,
                            accessRuleName,
                            accessRuleKey,
                            hostMapping.TargetHost,
                            targetHostAlias,
                            hostMapping.AllowedPorts,
                            hostMapping.AllowedPipes));
                }
            }
            else
            {
                string targetHostAlias = localHostName;
                if (string.Equals(targetHostAlias, "localhost", StringComparison.OrdinalIgnoreCase))
                {
                    targetHostAlias = localHostName;
                }
                host.Forwarders.Add(
                    new ServiceConnectionForwarder(
                        serviceNamespace,
                        accessRuleName,
                        accessRuleKey,
                        "localhost",
                        targetHostAlias,
                        permittedPorts,
                        string.Empty));
            }


            if (!runOnConsole)
            {
                ServiceController sc = new ServiceController("PortBridgeService");
                try
                {
                    var status = sc.Status;
                }
                catch (SystemException)
                {
                    runOnConsole = true;
                }
            }

            if (runOnConsole)
            {
                host.Open();
                Console.WriteLine("Press [ENTER] to exit.");
                Console.ReadLine();
                host.Close();
            }
            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                    new PortBridgeService(host)
                };
                ServiceBase.Run(ServicesToRun);
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("Arguments:");
            Console.WriteLine("\t-n <namespace> Relay Service Namespace");
            Console.WriteLine("\t-s <key> Relay Issuer Secret (Key)");
            Console.WriteLine("\t-a <port>[,<port>[...]] or '*' Allow connections on these ports");
            Console.WriteLine("\t-c Run service in console-mode");
        }

        static void PrintLogo()
        {
            Console.WriteLine("Port Bridge Service\n(c) Microsoft Corporation\n\n");
        }

        static bool ParseCommandLine(string[] args)
        {
            try
            {
                char lastOpt = default(char);

                foreach (var arg in args)
                {
                    if ((arg[0] == '-' || arg[0] == '/'))
                    {
                        if (lastOpt != default(char) || arg.Length != 2)
                        {
                            return false;
                        }
                        lastOpt = arg[1];
                        switch (lastOpt)
                        {
                            case 'c':
                            case 'C':
                                runOnConsole = true;
                                lastOpt = default(char);
                                break;
                        }
                        continue;
                    }

                    switch (lastOpt)
                    {
                        case 'M':
                        case 'm':
                            localHostName = arg;
                            lastOpt = default(char);
                            break;

                        case 'N':
                        case 'n':
                            serviceNamespace = arg;
                            lastOpt = default(char);
                            break;
                        case 'S':
                        case 's':
                            accessRuleKey = arg;
                            lastOpt = default(char);
                            break;
                        case 'A':
                        case 'a':
                            permittedPorts = arg;
                            lastOpt = default(char);
                            break;
                    }
                }

                if (lastOpt != default(char))
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
}