// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridgeServerAgent
{
    using System.Collections.Generic;
    using PortBridge;

    class PortBridgeServiceForwarderHost
    {
        public PortBridgeServiceForwarderHost()
        {
            Forwarders = new List<ServiceConnectionForwarder>();
        }

        public List<ServiceConnectionForwarder> Forwarders { get; }

        public void Open()
        {
            foreach (var forwarder in Forwarders)
            {
                forwarder.OpenService();
            }
        }

        public void Close()
        {
            foreach (var forwarder in Forwarders)
            {
                forwarder.CloseService();
            }
        }
    }
}