// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridgeClientAgent
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Net;
    using System.ServiceProcess;
    using PortBridge;

    class Program
    {
        static bool runOnConsole;
        static int fromPort = -1;
        static int toPort = -1;
        static string serviceNamespace;
        static string accessRuleName;
        static string accessRuleKey;
        static string cmdlineTargetHost;
        
        static void Main(string[] args)
        {
            PrintLogo();

            PortBridgeAgentSection settings = ConfigurationManager.GetSection("portBridgeAgent") as PortBridgeAgentSection;
            if (settings != null)
            {
                serviceNamespace = settings.ServiceNamespace;
                accessRuleName = settings.AccessRuleName;
                accessRuleKey = settings.AccessRuleKey;
            }

            if (!ParseCommandLine(args))
            {
                PrintUsage();
                return;
            }

            PortBridgeClientForwarderHost host = new PortBridgeClientForwarderHost();
            if (settings != null && settings.PortMappings.Count > 0)
            {
                foreach (PortMappingElement mapping in settings.PortMappings)
                {
                    List<IPRange> firewallRules = new List<IPRange>();
                    if (mapping.FirewallRules != null && mapping.FirewallRules.Count > 0)
                    {
                        foreach (FirewallRuleElement rule in mapping.FirewallRules)
                        {
                            if (!string.IsNullOrEmpty(rule.SourceRangeBegin) &&
                                !string.IsNullOrEmpty(rule.SourceRangeEnd))
                            {
                                firewallRules.Add(new IPRange(IPAddress.Parse(rule.SourceRangeBegin), IPAddress.Parse(rule.SourceRangeEnd)));
                            }
                            else if (!string.IsNullOrEmpty(rule.Source))
                            {
                                firewallRules.Add(new IPRange(IPAddress.Parse(rule.Source)));
                            }
                        }
                    }

                    if (mapping.LocalTcpPort.HasValue)
                    {
                        if (!string.IsNullOrEmpty(mapping.LocalPipe) ||
                            !string.IsNullOrEmpty(mapping.RemotePipe))
                        {
                            throw new ConfigurationErrorsException(
                                string.Format("LocalTcpPort {0} defined with incompatible other settings", mapping.LocalTcpPort.Value));
                        }
                        if (!mapping.RemoteTcpPort.HasValue)
                        {
                            throw new ConfigurationErrorsException(
                                string.Format("LocalTcpPort {0} does not have a matching RemoteTcpPort defined", mapping.LocalTcpPort.Value));
                        }

                        host.Forwarders.Add(
                            new TcpClientConnectionForwarder(
                                serviceNamespace,
                                accessRuleName,
                                accessRuleKey,
                                mapping.TargetHost,
                                mapping.LocalTcpPort.Value,
                                mapping.RemoteTcpPort.Value,
                                mapping.BindTo,
                                firewallRules));
                    }

                    if (!string.IsNullOrEmpty(mapping.LocalPipe))
                    {
                        if (mapping.LocalTcpPort.HasValue ||
                            mapping.RemoteTcpPort.HasValue)
                        {
                            throw new ConfigurationErrorsException(
                                string.Format("LocalPipe {0} defined with incompatible other settings", mapping.LocalPipe));
                        }
                        if (string.IsNullOrEmpty(mapping.RemotePipe))
                        {
                            throw new ConfigurationErrorsException(
                                string.Format("LocalPipe {0} does not have a matching RemotePipe defined", mapping.LocalPipe));
                        }

                        host.Forwarders.Add(
                            new NamedPipeClientConnectionForwarder(
                                serviceNamespace,
                                accessRuleName,
                                accessRuleKey,
                                mapping.TargetHost,
                                mapping.LocalPipe,
                                mapping.RemotePipe));
                    }
                }
            }
            else
            {
                List<IPRange> firewallRules = new List<IPRange>();
                firewallRules.Add(new IPRange(IPAddress.Loopback));
                host.Forwarders.Add(
                    new TcpClientConnectionForwarder(
                        serviceNamespace,
                        accessRuleName,
                        accessRuleKey,
                        cmdlineTargetHost,
                        fromPort,
                        toPort,
                        null,
                        firewallRules));
            }

            if (!runOnConsole)
            {
                ServiceController sc = new ServiceController("PortBridgeAgentService");
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
                    new PortBridgeAgentService(host)
                };
                ServiceBase.Run(ServicesToRun);
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("Arguments (all arguments are required):");
            Console.WriteLine("\t-n <namespace> Service Namespace");
            Console.WriteLine("\t-s <key> Issuer Secret (Key)");
            Console.WriteLine("\t-m <machine> mapped host name of the machine running the PortBridge service");
            Console.WriteLine("\t-l <port> Local TCP port number to map from");
            Console.WriteLine("\t-r <port> Remote TCP port number to map to");
        }

        static void PrintLogo()
        {
            Console.WriteLine("Port Bridge Agent\n(c) Microsoft Corporation\n\n");
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
                        case 'M':
                        case 'm':
                            cmdlineTargetHost = arg;
                            lastOpt = default(char);
                            break;
                        case 'L':
                        case 'l':
                            fromPort = int.Parse(arg);
                            lastOpt = default(char);
                            break;
                        case 'R':
                        case 'r':
                            toPort = int.Parse(arg);
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