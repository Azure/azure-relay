// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Relay.Bond.Epoxy
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Bond.Comm;
    using global::Bond.Comm.Service;
    using Microsoft.Azure.Relay;

    public class RelayEpoxyListener : Listener
    {
        readonly HashSet<RelayEpoxyConnection> connections;
        readonly object connectionsLock = new object();
        readonly HybridConnectionListener listener;
        readonly RelayEpoxyTransport parentTransport;
        readonly ServiceHost serviceHost;
        readonly CancellationTokenSource shutdownTokenSource;
        readonly TokenProvider tokenProvider;
        Task acceptTask;

        public RelayEpoxyListener(
            RelayEpoxyTransport parentTransport,
            string listenEndpoint,
            TokenProvider tokenProvider,
            Logger logger,
            Metrics metrics) : base(logger, metrics)
        {
            Debug.Assert(parentTransport != null);
            Debug.Assert(listenEndpoint != null);

            this.parentTransport = parentTransport;

            // will be null if not using TLS
            this.tokenProvider = tokenProvider;

            var endpoint = new Uri(listenEndpoint);
            listener = new HybridConnectionListener(endpoint, tokenProvider);
            serviceHost = new ServiceHost(logger);
            connections = new HashSet<RelayEpoxyConnection>();
            shutdownTokenSource = new CancellationTokenSource();
            ListenEndpoint = endpoint;
        }

        public Uri ListenEndpoint { get; }

        public override string ToString()
        {
            return $"EpoxyListener({ListenEndpoint})";
        }

        public override bool IsRegistered(string serviceMethodName)
        {
            return serviceHost.IsRegistered(serviceMethodName);
        }

        public override void AddService<T>(T service)
        {
            logger.Site().Information("Listener on {0} adding {1}.", ListenEndpoint, typeof(T).Name);
            serviceHost.Register(service);
        }

        public override void RemoveService<T>(T service)
        {
            throw new NotImplementedException();
        }

        public override async Task StartAsync()
        {
            await listener.OpenAsync(shutdownTokenSource.Token);
            acceptTask = Task.Run(() => AcceptAsync(shutdownTokenSource.Token), shutdownTokenSource.Token);
        }

        public override Task StopAsync()
        {
            var closeTask = listener.CloseAsync(TimeSpan.FromSeconds(10));
            shutdownTokenSource.Cancel();
            return acceptTask;
        }

        internal Error InvokeOnConnected(ConnectedEventArgs args)
        {
            return OnConnected(args);
        }

        internal void InvokeOnDisconnected(DisconnectedEventArgs args)
        {
            OnDisconnected(args);
        }

        async Task AcceptAsync(CancellationToken t)
        {
            logger.Site().Information("Accepting connections on {0}", ListenEndpoint);
            while (!t.IsCancellationRequested)
            {
                HybridConnectionStream connectionStream = null;
                try
                {
                    connectionStream = await listener.AcceptConnectionAsync();
                    logger.Site().Debug("Accepted connection from {0}.", listener.Address);

                    var connection = RelayEpoxyConnection.MakeServerConnection(
                        parentTransport,
                        this,
                        serviceHost,
                        connectionStream,
                        logger,
                        metrics);

                    lock (connectionsLock)
                    {
                        connections.Add(connection);
                    }

                    await connection.StartAsync();
                    logger.Site().Debug("Started server-side connection for {0}", connection);
                }
                catch (AuthenticationException ex)
                {
                    logger.Site().Error(ex, "Failed to authenticate remote connection from {0}", connectionStream);
                    ShutdownSocketSafe(connectionStream);
                }
                catch (SocketException ex)
                {
                    logger.Site().Error(ex, "Accept failed with error {0}.", ex.SocketErrorCode);
                    ShutdownSocketSafe(connectionStream);
                }
                catch (ObjectDisposedException)
                {
                    ShutdownSocketSafe(connectionStream);
                }
            }

            logger.Site().Information("Shutting down connection on {0}", ListenEndpoint);
        }

        static void ShutdownSocketSafe(HybridConnectionStream relayEpoxyStream)
        {
            relayEpoxyStream?.Shutdown();
        }
    }
}