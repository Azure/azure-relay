using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Microsoft.Azure.Relay.AspNetCore
{
    public class AzureRelayUrlPrefix
    {
        private Uri prefixUri;

        public AzureRelayUrlPrefix(string prefix, TokenProvider tokenProvider)
        {
            if ( !Uri.IsWellFormedUriString( prefix, UriKind.Absolute ))
            {
                try
                {
                    var rcb = new RelayConnectionStringBuilder(prefix);
                    this.prefixUri = string.IsNullOrEmpty(rcb.EntityPath)?rcb.Endpoint: new Uri(rcb.Endpoint, rcb.EntityPath);
                    if (!this.prefixUri.AbsolutePath.EndsWith("/"))
                    {
                        this.prefixUri = new UriBuilder(this.prefixUri) { Path = this.prefixUri.AbsolutePath + "/" }.Uri;
                    }
                    this.TokenProvider =
                        !string.IsNullOrEmpty(rcb.SharedAccessSignature) ? TokenProvider.CreateSharedAccessSignatureTokenProvider(rcb.SharedAccessSignature) :
                        !(string.IsNullOrEmpty(rcb.SharedAccessKeyName) || string.IsNullOrEmpty(rcb.SharedAccessKey)) ? TokenProvider.CreateSharedAccessSignatureTokenProvider(rcb.SharedAccessKeyName, rcb.SharedAccessKey) :
                        null;
                }
                catch (Exception e)
                {
                    throw new ArgumentException("prefix", e);
                }                
            }
            else if ( !Uri.TryCreate(prefix, UriKind.Absolute, out prefixUri))
            {
                throw new ArgumentException("prefix");
            }
            if (tokenProvider != null)
            {
                TokenProvider = tokenProvider;
            }
        }

        public static AzureRelayUrlPrefix Create(string prefix, TokenProvider tokenProvider = null)
        {
            return new AzureRelayUrlPrefix(prefix, tokenProvider);
        }

        public bool IsHttps => true;
        public string Scheme => "https";
        public string Host => prefixUri.Host;
        public string Path => prefixUri.AbsolutePath;
        public string FullPrefix => prefixUri.AbsoluteUri;
        public TokenProvider TokenProvider { get; private set; }

        public override bool Equals(object obj)
        {
            return string.Equals(FullPrefix, Convert.ToString(obj), StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(FullPrefix);
        }

        public override string ToString()
        {
            return FullPrefix;
        }
    }
}
