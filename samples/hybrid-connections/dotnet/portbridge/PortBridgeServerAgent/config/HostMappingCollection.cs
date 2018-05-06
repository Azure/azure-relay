// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridgeServerAgent
{
    using System;
    using System.Configuration;

    public class HostMappingCollection : ConfigurationElementCollection
    {
        public override ConfigurationElementCollectionType CollectionType
        {
            get { return ConfigurationElementCollectionType.AddRemoveClearMap; }
        }

        public HostMappingElement this[int index]
        {
            get { return (HostMappingElement) BaseGet(index); }
            set
            {
                if (BaseGet(index) != null)
                {
                    BaseRemoveAt(index);
                }
                BaseAdd(index, value);
            }
        }

        public new HostMappingElement this[string targetHost]
        {
            get { return (HostMappingElement) BaseGet(targetHost); }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new HostMappingElement();
        }

        protected override Object GetElementKey(ConfigurationElement element)
        {
            return ((HostMappingElement) element).TargetHost;
        }

        public int IndexOf(HostMappingElement hostMapping)
        {
            return BaseIndexOf(hostMapping);
        }

        public void Add(HostMappingElement hostMapping)
        {
            BaseAdd(hostMapping);
        }

        protected override void BaseAdd(ConfigurationElement element)
        {
            BaseAdd(element, false);
        }

        public void Remove(HostMappingElement hostMapping)
        {
            if (BaseIndexOf(hostMapping) >= 0)
            {
                BaseRemove(hostMapping.TargetHost);
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
    }
}