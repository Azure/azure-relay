// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridgeClientAgent
{
    using System.ServiceProcess;

    partial class PortBridgeAgentService : ServiceBase
    {
        readonly PortBridgeClientForwarderHost host;

        public PortBridgeAgentService(PortBridgeClientForwarderHost host)
        {
            this.host = host;
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            host.Open();
        }

        protected override void OnStop()
        {
            host.Close();
        }
    }
}