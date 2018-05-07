using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Microsoft.Azure.Relay.AspNetCore
{
    public class AzureRelayUrlPrefix
    {
        private AzureRelayUrlPrefix(bool isHttps, string scheme, string host, string path, TokenProvider tokenProvider)
        {
            IsHttps = isHttps;
            Scheme = scheme;
            Host = host;
            Path = path;
            FullPrefix = string.Format(CultureInfo.InvariantCulture, "{0}://{1}{2}", Scheme, Host, Path);
            TokenProvider = tokenProvider;
        }
        
        /// <summary>
        /// http://msdn.microsoft.com/en-us/library/windows/desktop/aa364698(v=vs.85).aspx
        /// </summary>
        /// <param name="scheme">http or https. Will be normalized to lower case.</param>
        /// <param name="host">+, *, IPv4, [IPv6], or a dns name. Http.Sys does not permit punycode (xn--), use Unicode instead.</param>
        /// <param name="path">Should start and end with a '/', though a missing trailing slash will be added. This value must be un-escaped.</param>
        /// <param name="tokenProvider"></param>
        private static AzureRelayUrlPrefix Create(string scheme, string host, string path, TokenProvider tokenProvider)
        {
            bool isHttps;
            if (string.Equals(Uri.UriSchemeHttp, scheme, StringComparison.OrdinalIgnoreCase))
            {
                scheme = Uri.UriSchemeHttp; // Always use a lower case scheme
                isHttps = false;
            }
            else if (string.Equals(Uri.UriSchemeHttps, scheme, StringComparison.OrdinalIgnoreCase))
            {
                scheme = Uri.UriSchemeHttps; // Always use a lower case scheme
                isHttps = true;
            }
            else
            {
                throw new ArgumentOutOfRangeException("scheme", scheme, Resources.Exception_UnsupportedScheme);
            }

            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentNullException("host");
            }
            
            // Http.Sys requires the path end with a slash.
            if (string.IsNullOrWhiteSpace(path))
            {
                path = "/";
            }
            else if (!path.EndsWith("/", StringComparison.Ordinal))
            {
                path += "/";
            }

            return new AzureRelayUrlPrefix(isHttps, scheme, host, path, tokenProvider);
        }

        public static AzureRelayUrlPrefix Create(string prefix, TokenProvider tokenProvider = null)
        {
            string scheme = null;
            string host = null;
            string path = null;
            var whole = prefix ?? string.Empty;

            var schemeDelimiterEnd = whole.IndexOf("://", StringComparison.Ordinal);
            if (schemeDelimiterEnd < 0)
            {
                throw new FormatException("Invalid prefix, missing scheme separator: " + prefix);
            }
            var hostDelimiterStart = schemeDelimiterEnd + "://".Length;

            var pathDelimiterStart = whole.IndexOf("/", hostDelimiterStart, StringComparison.Ordinal);
            if (pathDelimiterStart < 0)
            {
                pathDelimiterStart = whole.Length;
            }
            var hostDelimiterEnd = whole.LastIndexOf(":", pathDelimiterStart - 1, pathDelimiterStart - hostDelimiterStart, StringComparison.Ordinal);
            if (hostDelimiterEnd < 0)
            {
                hostDelimiterEnd = pathDelimiterStart;
            }

            scheme = whole.Substring(0, schemeDelimiterEnd);
            host = whole.Substring(hostDelimiterStart, pathDelimiterStart - hostDelimiterStart);
            path = whole.Substring(pathDelimiterStart);

            return Create(scheme, host, path, tokenProvider);
        }

        public bool IsHttps { get; private set; }
        public string Scheme { get; private set; }
        public string Host { get; private set; }
        public string Path { get; private set; }
        public string FullPrefix { get; private set; }
        public TokenProvider TokenProvider { get; set; }

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
