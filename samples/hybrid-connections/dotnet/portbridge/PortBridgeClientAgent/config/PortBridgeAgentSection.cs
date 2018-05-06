// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridgeClientAgent
{
    using System.Configuration;

    class PortBridgeAgentSection : ConfigurationSection
    {
        internal const string portMappingsString = "portMappings";
        internal const string serviceBusNamespaceString = "serviceBusNamespace";
        internal const string serviceBusAccessRuleNameString = "serviceBusAccessRuleName";
        internal const string serviceBusAccessRuleKeyString = "serviceBusAccessRuleKey";

        [ConfigurationProperty(serviceBusNamespaceString, DefaultValue = null, IsRequired = true)]
        public string ServiceNamespace
        {
            get { return (string) this[serviceBusNamespaceString]; }
            set { this[serviceBusNamespaceString] = value; }
        }

        [ConfigurationProperty(serviceBusAccessRuleNameString, DefaultValue = "owner", IsRequired = false)]
        public string AccessRuleName
        {
            get { return (string) this[serviceBusAccessRuleNameString]; }
            set { this[serviceBusAccessRuleNameString] = value; }
        }

        [ConfigurationProperty(serviceBusAccessRuleKeyString, DefaultValue = null, IsRequired = true)]
        public string AccessRuleKey
        {
            get { return (string) this[serviceBusAccessRuleKeyString]; }
            set { this[serviceBusAccessRuleKeyString] = value; }
        }

        [ConfigurationProperty(portMappingsString, IsDefaultCollection = false)]
        [ConfigurationCollection(typeof (PortMappingCollection), AddItemName = "port")]
        public PortMappingCollection PortMappings
        {
            get { return (PortMappingCollection) base[portMappingsString]; }
        }
    }
}