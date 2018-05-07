using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Relay;

namespace Microsoft.Azure.Relay.AspNetCore
{
    class RequestContext : IDisposable
    {
        private CancellationTokenSource _cts;
        private RelayedHttpListenerContext _innerContext;
        private Response _response;
        private Request _request;

        public RequestContext(RelayedHttpListenerContext innerContext, Uri baseUri)
        {
            _cts = new CancellationTokenSource();
            _innerContext = innerContext;
            _response = new Response(_innerContext.Response, baseUri);
            _request = new Request( _innerContext.Request, baseUri);
            IsUpgradableRequest = false;
        }

        public TrackingContext TrackingContext => _innerContext.TrackingContext;
        public Request Request { get { return _request; } }
        public Response Response { get { return _response; } }

        public CancellationToken DisconnectToken { get { return _cts.Token; } }
        public bool IsUpgradableRequest { get; internal set; }

        public void Dispose()
        {
            Response.Close();
        }

        internal Task<Stream> UpgradeAsync()
        {
            throw new NotImplementedException();
        }

        internal void Abort()
        {
            throw new NotImplementedException();
        }
    }
}
