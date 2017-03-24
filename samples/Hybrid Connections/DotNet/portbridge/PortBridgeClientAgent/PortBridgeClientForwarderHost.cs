// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridgeClientAgent
{
    using System.Collections.Generic;
    using PortBridge;

    class PortBridgeClientForwarderHost
    {
        public PortBridgeClientForwarderHost()
        {
            Forwarders = new List<IClientConnectionForwarder>();
        }

        public List<IClientConnectionForwarder> Forwarders { get; }

        public void Open()
        {
            foreach (var forwarder in Forwarders)
            {
                forwarder.Open();
            }
        }

        public void Close()
        {
            foreach (var forwarder in Forwarders)
            {
                forwarder.Close();
            }
        }
    }
}