using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.Relay.AspNetCore
{
    class Response
    {
        private readonly RelayedHttpListenerResponse _innerResponse;

        public Response(RelayedHttpListenerResponse innerResponse, Uri baseUri)
        {
            _innerResponse = innerResponse;
            Headers = new HeaderCollection(new WebHeaderCollectionWrapper(_innerResponse.Headers));
            foreach (var hdr in innerResponse.Headers.AllKeys)
            {
                Headers.Append(hdr, innerResponse.Headers[hdr]);
            }
        }

        public HeaderCollection Headers { get; set; }

        public Stream Body => _innerResponse.OutputStream;

        public int StatusCode
        {
            get
            {
                return (int)_innerResponse.StatusCode;
            }
            set
            {
                if (value <= 100 || value > 999)
                {
                    throw new ArgumentOutOfRangeException();
                }
                _innerResponse.StatusCode = (HttpStatusCode)value;
            }
        }

        public string ReasonPhrase
        {
            get
            {
                return _innerResponse.StatusDescription;
            }
            set
            {
                _innerResponse.StatusDescription = value;
            }
        }

        public long? ContentLength { get; internal set; }

        internal async Task SendFileAsync(string path, long offset, long? length, CancellationToken cancellationToken)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(path);
            }
            using (var fs = File.OpenRead(path))
            {
                if (length.HasValue && length > fs.Length)
                {
                    throw new ArgumentOutOfRangeException("length");
                }
                if (offset > fs.Length)
                {
                    throw new ArgumentOutOfRangeException("offset");
                }
                long len = length.HasValue ? length.Value : fs.Length;
                byte[] buffer = new byte[81920];
                fs.Seek(offset, SeekOrigin.Begin);
                long sent = 0;
                do
                {
                    int read = await fs.ReadAsync(buffer, 0, (int)Math.Min((long)buffer.Length, Math.Min(len - sent, int.MaxValue)), cancellationToken);
                    if (read == 0)
                    {
                        break;
                    }
                    await this.Body.WriteAsync(buffer, 0, read);
                    sent += read;
                }
                while (sent < len);
            }
        }

        public TimeSpan CacheTtl { get; internal set; }
        public bool HasStarted { get; internal set; }

        public void Close()
        {
            _innerResponse.Close();
        }

        public Task CloseAsync()
        {
            return _innerResponse.CloseAsync();
        }

        private class WebHeaderCollectionWrapper : IDictionary<string, StringValues>
        {
            private readonly WebHeaderCollection _webHeaderCollection;

            public WebHeaderCollectionWrapper(WebHeaderCollection webHeaderCollection)
            {
                _webHeaderCollection = webHeaderCollection;
            }

            public StringValues this[string key] { get => _webHeaderCollection[key]; set => _webHeaderCollection[key] = value; }

            public ICollection<string> Keys => _webHeaderCollection.AllKeys;

            public ICollection<StringValues> Values =>
                _webHeaderCollection.AllKeys.Select(k => new StringValues(_webHeaderCollection.GetValues(k))).ToList();

            public int Count => _webHeaderCollection.Count;

            public bool IsReadOnly => false;

            public void Add(string key, StringValues value)
            {
                _webHeaderCollection[key] = value;
            }

            public void Add(KeyValuePair<string, StringValues> item)
            {
                _webHeaderCollection[item.Key] = item.Value;
            }

            public void Clear()
            {
                _webHeaderCollection.Clear();
            }

            public bool Contains(KeyValuePair<string, StringValues> item)
            {
                return _webHeaderCollection[item.Key] == item.Value;
            }

            public bool ContainsKey(string key)
            {
                return _webHeaderCollection.AllKeys.Contains(key);
            }

            public void CopyTo(KeyValuePair<string, StringValues>[] array, int arrayIndex)
            {
                for (var i = 0; i < _webHeaderCollection.Count; i++)
                {
                    array[arrayIndex + i] = new KeyValuePair<string, StringValues>(
                        _webHeaderCollection.GetKey(i),
                        new StringValues(_webHeaderCollection.GetValues(i)));
                }
            }

            public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator()
            {
                for (var i = 0; i < _webHeaderCollection.Count; i++)
                {
                    yield return new KeyValuePair<string, StringValues>(
                        _webHeaderCollection.GetKey(i),
                        new StringValues(_webHeaderCollection.GetValues(i)));
                }
            }

            public bool Remove(string key)
            {
                if (!ContainsKey(key)) return false;
                _webHeaderCollection.Remove(key);
                return true;
            }

            public bool Remove(KeyValuePair<string, StringValues> item)
            {
                if (!Contains(item)) return false;
                _webHeaderCollection.Remove(item.Key);
                return true;
            }

            public bool TryGetValue(string key, out StringValues value)
            {
                if (!ContainsKey(key)) return false;
                value = new StringValues(_webHeaderCollection[key]);
                return true;
            }

            IEnumerator IEnumerable.GetEnumerator()
                => GetEnumerator();
        }
    }
}
