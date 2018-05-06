// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Relay.Bond.Epoxy
{
    using global::Bond.Comm;

    public class RelayEpoxySendContext : SendContext
    {
        public RelayEpoxySendContext(RelayEpoxyConnection connection, ConnectionMetrics connectionMetrics, RequestMetrics requestMetrics)
            : base(connectionMetrics, requestMetrics)
        {
            Connection = connection;
        }

        public override Connection Connection { get; }
    }

    public class RelayEpoxyReceiveContext : ReceiveContext
    {
        public RelayEpoxyReceiveContext(RelayEpoxyConnection connection, ConnectionMetrics connectionMetrics, RequestMetrics requestMetrics)
            : base(connectionMetrics, requestMetrics)
        {
            Connection = connection;
        }

        public override Connection Connection { get; }
    }
}