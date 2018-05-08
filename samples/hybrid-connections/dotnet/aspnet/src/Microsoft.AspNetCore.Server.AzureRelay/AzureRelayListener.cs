// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Relay;
using System.Threading.Tasks.Dataflow;

namespace Microsoft.Azure.Relay.AspNetCore
{
    /// <summary>
    /// An HTTP server wrapping the Http.Sys APIs that accepts requests.
    /// </summary>
    internal class AzureRelayListener : IDisposable
    {
        private List<HybridConnectionListener> _relayListeners = new List<HybridConnectionListener>();
        private State _state = State.Stopped;
        private object _internalLock = new object();
        private BufferBlock<RequestContext> _pendingContexts;
        private Action<RequestContext> requestHandler;

        public AzureRelayListener(AzureRelayOptions options, ILoggerFactory loggerFactory)
            : this(options, loggerFactory, true)
        {
            this.requestHandler = HandleRequest;
        }

        public AzureRelayListener(AzureRelayOptions options, ILoggerFactory loggerFactory, Action<RequestContext> callback)
            : this(options, loggerFactory, true)
        {
            this.requestHandler = callback;
        }


        private AzureRelayListener(AzureRelayOptions options, ILoggerFactory loggerFactory, bool priv)
        {
            _pendingContexts = new BufferBlock<RequestContext>();
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            Options = options;

            Logger = LogHelper.CreateLogger(loggerFactory, typeof(AzureRelayListener));
        }

        
        private Task<bool> WebSocketAcceptHandler(RelayedHttpListenerContext arg)
        {
            return Task<bool>.FromResult(true);
        }

        private void HandleRequest(RequestContext request)
        {
            _pendingContexts.Post(request);
        }

        public Task<RequestContext> AcceptAsync()
        {
            return _pendingContexts.ReceiveAsync();
        }

        internal enum State
        {
            Stopped,
            Started,
            Disposed,
        }

        internal ILogger Logger { get; private set; }

        public AzureRelayOptions Options { get; }

        public bool IsListening
        {
            get { return _state == State.Started; }
        }
        

        /// <summary>
        /// Start accepting incoming requests.
        /// </summary>
        public void Start()
        {
            CheckDisposed();

            LogHelper.LogInfo(Logger, "Start");

            // Make sure there are no race conditions between Start/Stop/Abort/Close/Dispose.
            // Start needs to setup all resources. Abort/Stop must not interfere while Start is
            // allocating those resources.
            lock (_internalLock)
            {
                try
                {
                    CheckDisposed();
                    if (_state == State.Started)
                    {
                        return;
                    }

                    try
                    {
                        foreach (var urlPrefix in Options.UrlPrefixes)
                        {
                            RelayConnectionStringBuilder rcb = new RelayConnectionStringBuilder();
                            
                            var relayListener = new HybridConnectionListener(
                                new UriBuilder(urlPrefix.FullPrefix) { Scheme = "sb", Port = -1 }.Uri,
                                urlPrefix.TokenProvider != null ? urlPrefix.TokenProvider : Options.TokenProvider);

                            relayListener.RequestHandler = (ctx) => requestHandler(new RequestContext(ctx, new Uri(urlPrefix.FullPrefix)));
                            relayListener.AcceptHandler = WebSocketAcceptHandler;
                            _relayListeners.Add(relayListener);
                        }
                    }
                    catch (Exception exception)
                    {
                        LogHelper.LogException(Logger, ".Ctor", exception);
                        throw;
                    }


                    foreach (var listener in _relayListeners)
                    {
                        listener.OpenAsync().GetAwaiter().GetResult();
                        //listener.AcceptConnectionAsync().ContinueWith((t) => {
                        //    Console.WriteLine(t);
                        //});
                    }
                    _state = State.Started;
                }
                catch (Exception exception)
                {
                    // Make sure the HttpListener instance can't be used if Start() failed.
                    _state = State.Disposed;
                    LogHelper.LogException(Logger, "Start", exception);
                    throw;
                }
            }
        }

        private void Stop()
        {
            try
            {
                lock (_internalLock)
                {
                    CheckDisposed();
                    if (_state == State.Stopped)
                    {
                        return;
                    }

                    _state = State.Stopped;

                    foreach (var listener in _relayListeners)
                    {
                        listener.CloseAsync().GetAwaiter().GetResult();
                    }
                }
            }
            catch (Exception exception)
            {
                LogHelper.LogException(Logger, "Stop", exception);
                throw;
            }
        }

        /// <summary>
        /// Stop the server and clean up.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            lock (_internalLock)
            {
                try
                {
                    if (_state == State.Disposed)
                    {
                        return;
                    }
                    LogHelper.LogInfo(Logger, "Dispose");

                    Stop();
                }
                catch (Exception exception)
                {
                    LogHelper.LogException(Logger, "Dispose", exception);
                    throw;
                }
                finally
                {
                    _state = State.Disposed;
                }
            }
        }


        private void CheckDisposed()
        {
            if (_state == State.Disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }
    }
}
