// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridgeClientAgent
{
    using System.Configuration;

    public class FirewallRuleCollection : ConfigurationElementCollection
    {
        public override ConfigurationElementCollectionType CollectionType
        {
            get { return ConfigurationElementCollectionType.BasicMap; }
        }

        public FirewallRuleElement this[int index]
        {
            get { return (FirewallRuleElement) BaseGet(index); }
            set
            {
                if (BaseGet(index) != null)
                {
                    BaseRemoveAt(index);
                }
                BaseAdd(index, value);
            }
        }

        protected override string ElementName
        {
            get { return "rule"; }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new FirewallRuleElement();
        }

        public int IndexOf(FirewallRuleElement FirewallRule)
        {
            return BaseIndexOf(FirewallRule);
        }

        public void Add(FirewallRuleElement FirewallRule)
        {
            BaseAdd(FirewallRule);
        }

        protected override void BaseAdd(ConfigurationElement element)
        {
            BaseAdd(element, false);
        }

        public void Remove(FirewallRuleElement FirewallRule)
        {
            if (BaseIndexOf(FirewallRule) >= 0)
            {
                BaseRemove(FirewallRule);
            }
        }

        public void RemoveAt(int index)
        {
            BaseRemoveAt(index);
        }

        public void Remove(string targetHost)
        {
            BaseRemove(targetHost);
        }

        public void Clear()
        {
            BaseClear();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return element;
        }
    }
}