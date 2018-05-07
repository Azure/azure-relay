// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Microsoft.Azure.Relay.AspNetCore
{
    [SuppressMessage("Microsoft.Usage", "CA2237:MarkISerializableTypesWithSerializable")]
    public class AzureRelayException : Exception
    {
        internal AzureRelayException()
            : base()
        {
        }

        internal AzureRelayException(int errorCode)
            : base()
        {
            ErrorCode = errorCode;
        }

        internal AzureRelayException(int errorCode, string message)
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public int ErrorCode { get; }
    }
}
