using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Relay;

namespace Microsoft.Azure.Relay.AspNetCore
{
    class Request
    {
        private RelayedHttpListenerRequest _innerRequest;
        private readonly Uri _baseUri;
        private readonly Uri _requestUri;
        HeaderCollection _headers;

        public Request(RelayedHttpListenerRequest innerRequest, Uri baseUri)
        {
            _innerRequest = innerRequest;
            this._requestUri = new UriBuilder(innerRequest.Url) { Scheme = "https" }.Uri;
            this._baseUri = baseUri;
            _headers = new HeaderCollection();
            foreach (var hdr in innerRequest.Headers.AllKeys)
            {
                if (!string.IsNullOrWhiteSpace(innerRequest.Headers[hdr]))
                {
                    _headers.Append(hdr, innerRequest.Headers[hdr]);
                }
            }
            this.ProtocolVersion = new Version(1, 1);
        }

        public Uri Url => _requestUri;
        public IPEndPoint RemoteEndpoint => _innerRequest.RemoteEndPoint;
        public Stream Body => _innerRequest.InputStream;
        public string Method => _innerRequest.HttpMethod;
        public HeaderCollection Headers => _headers;
        public bool HasEntityBody => _innerRequest.HasEntityBody;

        public string Scheme => "https";
        public string Path => Url.AbsolutePath.Substring(_baseUri.AbsolutePath.Length-1);
        public string PathBase => _baseUri.AbsolutePath;
        public string QueryString => Url.Query;

        public Version ProtocolVersion { get; internal set; }
        public IPAddress RemoteIpAddress { get; internal set; }
        public int RemotePort { get; internal set; }
        public IPAddress LocalIpAddress { get; internal set; }
        public int LocalPort { get; internal set; }
        public string RawUrl => Url.AbsoluteUri;

        internal Task<object> GetClientCertificateAsync()
        {
            return Task.FromResult((object)null);
        }
    }
}
