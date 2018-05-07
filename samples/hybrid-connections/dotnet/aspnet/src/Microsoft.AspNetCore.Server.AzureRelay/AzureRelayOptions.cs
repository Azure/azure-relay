// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Azure.Relay;

namespace Microsoft.Azure.Relay.AspNetCore
{
    public class AzureRelayOptions
    {
        public AzureRelayOptions()
        {

        }

        public TokenProvider TokenProvider { get; set; }
        internal bool ThrowWriteExceptions { get; set; }
        internal long MaxRequestBodySize { get; set; }
        internal int RequestQueueLimit { get; set; }
        public AzureRelayUrlPrefixCollection UrlPrefixes { get; } = new AzureRelayUrlPrefixCollection();
        internal int? MaxConnections { get; set; }
        
    }
}
