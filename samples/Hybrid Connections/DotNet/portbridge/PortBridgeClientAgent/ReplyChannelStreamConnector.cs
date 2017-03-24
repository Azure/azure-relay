// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridge
{
    using System;
    using System.IO;
    using Microsoft.ServiceBus.Samples.Sockets;

    class ReplyChannelStreamConnector : IDataExchange
    {
        Stream stream;

        public ReplyChannelStreamConnector(Stream stream)
        {
            this.stream = stream;
        }

        public void Connect(string connectionInfo)
        {
        }

        public void Write(TransferBuffer data)
        {
            if (data.Data.Array != null)
            {
                this.stream.Write(data.Data.Array, data.Data.Offset, data.Data.Count);
            }
        }

        public void Disconnect()
        {
        }
    }
}
