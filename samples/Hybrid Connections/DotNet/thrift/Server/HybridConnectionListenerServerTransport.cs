// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Server
{
    using System.Threading;
    using Microsoft.Azure.Relay;
    using Thrift.Transport;

    public class HybridConnectionListenerServerTransport : TServerTransport
    {
        readonly HybridConnectionListener listener;

        public HybridConnectionListenerServerTransport(HybridConnectionListener listener)
        {
            this.listener = listener;
        }

        public override void Listen()
        {
            listener.OpenAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        public override void Close()
        {
            listener.CloseAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        protected override TTransport AcceptImpl()
        {
            var acceptedConnection = listener.AcceptConnectionAsync().GetAwaiter().GetResult();
            return new TStreamTransport(acceptedConnection, acceptedConnection);
        }
    }
}