// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridgeServerAgent
{
    using System.ServiceProcess;

    partial class PortBridgeService : ServiceBase
    {
        readonly PortBridgeServiceForwarderHost host;

        public PortBridgeService(PortBridgeServiceForwarderHost host)
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