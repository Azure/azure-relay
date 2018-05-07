using Microsoft.Extensions.Logging;
using System;

namespace Microsoft.Azure.Relay.AspNetCore
{
    internal class UrlGroup : IDisposable
    {
        private bool _disposed;
        private ILogger _logger;

        internal UrlGroup(ILogger logger)
        {
            _logger = logger;

            ulong urlGroupId = 0;
            Id = urlGroupId;
        }

        internal ulong Id { get; private set; }

        internal unsafe void SetMaxConnections(long maxConnections)
        {

        }

        internal void RegisterPrefix(string uriPrefix, int contextId)
        {
            LogHelper.LogInfo(_logger, "Listening on prefix: " + uriPrefix);
            CheckDisposed();


        }

        internal bool UnregisterPrefix(string uriPrefix)
        {
            LogHelper.LogInfo(_logger, "Stop listening on prefix: " + uriPrefix);
            CheckDisposed();
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            Id = 0;
        }

        private void CheckDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }
    }
}
