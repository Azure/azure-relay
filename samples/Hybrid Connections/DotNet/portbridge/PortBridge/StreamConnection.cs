// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridge
{
    using System.IO;

    public class StreamConnection
    {
        public StreamConnection(Stream stream, string connectionInfo)
        {
            Stream = stream;
            ConnectionInfo = connectionInfo;
        }

        public Stream Stream { get; }
        public string ConnectionInfo { get; }
    }
}