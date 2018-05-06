// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Relay.Bond.Epoxy
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Bond.Comm;
    using global::Bond.Comm.Service;
    using Microsoft.Azure.Relay;

    public class RelayEpoxyTransportBuilder : TransportBuilder<RelayEpoxyTransport>
    {
        readonly TokenProvider tokenProvider;

        public RelayEpoxyTransportBuilder(TokenProvider tokenProvider)
        {
            this.tokenProvider = tokenProvider;
        }

        public override RelayEpoxyTransport Construct()
        {
            return new RelayEpoxyTransport(
                tokenProvider,
                LayerStackProvider,
                LogSink,
                EnableDebugLogs,
                MetricsSink);
        }
    }

    public class RelayEpoxyTransport : Transport<RelayEpoxyConnection, RelayEpoxyListener>
    {
        readonly ILayerStackProvider layerStackProvider;
        readonly Logger logger;
        readonly Metrics metrics;
        readonly TokenProvider tokenProvider;

        public RelayEpoxyTransport(
            TokenProvider tokenProvider,
            ILayerStackProvider layerStackProvider,
            ILogSink logSink,
            bool enableDebugLogs,
            IMetricsSink metricsSink)
        {
            // Layer stack provider may be null
            this.tokenProvider = tokenProvider;
            this.layerStackProvider = layerStackProvider;

            // Log sink may be null
            logger = new Logger(logSink, enableDebugLogs);
            // Metrics sink may be null
            metrics = new Metrics(metricsSink);
        }

        public override Error GetLayerStack(string uniqueId, out ILayerStack stack)
        {
            if (layerStackProvider != null)
            {
                return layerStackProvider.GetLayerStack(uniqueId, out stack);
            }
            stack = null;
            return null;
        }

        /// <param name="address">A URI with a scheme of epoxy:// (insecure epoxy) or epoxys:// (epoxy over TLS).</param>
        public override Task<RelayEpoxyConnection> ConnectToAsync(string address)
        {
            return ConnectToAsync(address, CancellationToken.None);
        }

        public override async Task<RelayEpoxyConnection> ConnectToAsync(string address, CancellationToken ct)
        {
            logger.Site().Information("Connecting to {0}.", address);

            HybridConnectionStream socket = await ConnectClientSocketAsync(new Uri(address));

            var connection = RelayEpoxyConnection.MakeClientConnection(this, socket, logger, metrics);
            await connection.StartAsync();
            return connection;
        }

        public override RelayEpoxyListener MakeListener(string address)
        {
            return new RelayEpoxyListener(this, address, tokenProvider, logger, metrics);
        }

        public override Task StopAsync()
        {
            return TaskExt.CompletedTask;
        }

        Task<HybridConnectionStream> ConnectClientSocketAsync(Uri address)
        {
            var client = new HybridConnectionClient(address, tokenProvider);
            return client.CreateConnectionAsync();
        }
    }
}