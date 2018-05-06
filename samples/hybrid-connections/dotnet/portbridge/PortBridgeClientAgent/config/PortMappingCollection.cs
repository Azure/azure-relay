// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridgeClientAgent
{
    using System.Configuration;

    public class PortMappingCollection : ConfigurationElementCollection
    {
        public override ConfigurationElementCollectionType CollectionType
        {
            get { return ConfigurationElementCollectionType.BasicMap; }
        }

        public PortMappingElement this[int index]
        {
            get { return (PortMappingElement) BaseGet(index); }
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
            get { return "port"; }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new PortMappingElement();
        }

        public int IndexOf(PortMappingElement PortMapping)
        {
            return BaseIndexOf(PortMapping);
        }

        public void Add(PortMappingElement PortMapping)
        {
            BaseAdd(PortMapping);
        }

        protected override void BaseAdd(ConfigurationElement element)
        {
            BaseAdd(element, false);
        }

        public void Remove(PortMappingElement PortMapping)
        {
            if (BaseIndexOf(PortMapping) >= 0)
            {
                BaseRemove(PortMapping);
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