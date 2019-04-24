// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridgeServerAgent
{
    using System.ComponentModel;
    using System.Configuration.Install;

    [RunInstaller(true)]
    public partial class ServerServiceInstaller : Installer
    {
        public ServerServiceInstaller()
        {
            InitializeComponent();
        }
    }
}