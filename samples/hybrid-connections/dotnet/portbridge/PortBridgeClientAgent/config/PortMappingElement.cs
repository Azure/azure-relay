// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridgeClientAgent
{
    using System.Configuration;

    public class PortMappingElement : ConfigurationElement
    {
        internal const string localTcpPortString = "localTcpPort";
        internal const string remoteTcpPortString = "remoteTcpPort";
        internal const string localPipeString = "localPipe";
        internal const string remotePipeString = "remotePipe";
        internal const string targetHostString = "targetHost";
        internal const string bindToString = "bindTo";
        internal const string firewallRules = "firewallRules";

        public PortMappingElement(string targetHost, int localTcpPort, int remoteTcpPort, string bindTo)
        {
            TargetHost = targetHost;
            LocalTcpPort = localTcpPort;
            RemoteTcpPort = remoteTcpPort;
            BindTo = bindTo;
        }

        public PortMappingElement()
        {
        }

        [ConfigurationProperty(targetHostString, IsRequired = true)]
        public string TargetHost
        {
            get { return (string) this[targetHostString]; }
            set { this[targetHostString] = value; }
        }

        [ConfigurationProperty(localTcpPortString, IsRequired = false)]
        public int? LocalTcpPort
        {
            get { return (int?) this[localTcpPortString]; }
            set { this[localTcpPortString] = value; }
        }

        [ConfigurationProperty(remoteTcpPortString, IsRequired = false)]
        public int? RemoteTcpPort
        {
            get { return (int?) this[remoteTcpPortString]; }
            set { this[remoteTcpPortString] = value; }
        }

        [ConfigurationProperty(localPipeString, IsRequired = false)]
        public string LocalPipe
        {
            get { return (string) this[localPipeString]; }
            set { this[localPipeString] = value; }
        }

        [ConfigurationProperty(remotePipeString, IsRequired = false)]
        public string RemotePipe
        {
            get { return (string) this[remotePipeString]; }
            set { this[remotePipeString] = value; }
        }

        [ConfigurationProperty(bindToString, IsRequired = false)]
        public string BindTo
        {
            get { return (string) this[bindToString]; }
            set { this[bindToString] = value; }
        }

        [ConfigurationProperty(firewallRules, IsDefaultCollection = false)]
        [ConfigurationCollection(typeof (FirewallRuleCollection), AddItemName = "allow")]
        public FirewallRuleCollection FirewallRules
        {
            get { return (FirewallRuleCollection) base[firewallRules]; }
        }
    }
}