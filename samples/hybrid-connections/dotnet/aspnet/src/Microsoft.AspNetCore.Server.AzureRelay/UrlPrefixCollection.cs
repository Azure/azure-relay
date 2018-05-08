
namespace Microsoft.Azure.Relay.AspNetCore
{
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// A collection or URL prefixes
    /// </summary>
    public class AzureRelayUrlPrefixCollection : ICollection<AzureRelayUrlPrefix>
    {
        private readonly IDictionary<int, AzureRelayUrlPrefix> _prefixes = new Dictionary<int, AzureRelayUrlPrefix>(1);
        private UrlGroup _urlGroup;
        private int _nextId = 1;

        internal AzureRelayUrlPrefixCollection()
        {
        }

        public int Count
        {
            get
            {
                lock (_prefixes)
                {
                    return _prefixes.Count;
                }
            }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public void Add(string connectionString, string path = null, TokenProvider tokenProvider = null)
        {
            Add(AzureRelayUrlPrefix.Create(connectionString, null));
        }

        public void Add(string prefix, TokenProvider tokenProvider)
        {
            Add(AzureRelayUrlPrefix.Create(prefix, tokenProvider));
        }

        public void Add(AzureRelayUrlPrefix item)
        {
            lock (_prefixes)
            {
                var id = _nextId++;
                if (_urlGroup != null)
                {
                    _urlGroup.RegisterPrefix(item.FullPrefix, id);
                }
                _prefixes.Add(id, item);
            }
        }

        internal AzureRelayUrlPrefix GetPrefix(int id)
        {
            lock (_prefixes)
            {
                return _prefixes[id];
            }
        }

        public void Clear()
        {
            lock (_prefixes)
            {
                if (_urlGroup != null)
                {
                    UnregisterAllPrefixes();
                }
                _prefixes.Clear();
            }
        }

        public bool Contains(AzureRelayUrlPrefix item)
        {
            lock (_prefixes)
            {
                return _prefixes.Values.Contains(item);
            }
        }

        public void CopyTo(AzureRelayUrlPrefix[] array, int arrayIndex)
        {
            lock (_prefixes)
            {
                _prefixes.Values.CopyTo(array, arrayIndex);
            }
        }

        public bool Remove(string prefix)
        {
            return Remove(AzureRelayUrlPrefix.Create(prefix, null));
        }

        public bool Remove(AzureRelayUrlPrefix item)
        {
            lock (_prefixes)
            {
                int? id = null;
                foreach (var pair in _prefixes)
                {
                    if (pair.Value.Equals(item))
                    {
                        id = pair.Key;
                        if (_urlGroup != null)
                        {
                            _urlGroup.UnregisterPrefix(pair.Value.FullPrefix);
                        }
                    }
                }
                if (id.HasValue)
                {
                    _prefixes.Remove(id.Value);
                    return true;
                }
                return false;
            }
        }

        public IEnumerator<AzureRelayUrlPrefix> GetEnumerator()
        {
            lock (_prefixes)
            {
                return _prefixes.Values.GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        internal void RegisterAllPrefixes(UrlGroup urlGroup)
        {
            lock (_prefixes)
            {
                _urlGroup = urlGroup;
                // go through the uri list and register for each one of them
                foreach (var pair in _prefixes)
                {
                    // We'll get this index back on each request and use it to look up the prefix to calculate PathBase.
                    _urlGroup.RegisterPrefix(pair.Value.FullPrefix, pair.Key);
                }
            }
        }

        internal void UnregisterAllPrefixes()
        {
            lock (_prefixes)
            {
                // go through the uri list and unregister for each one of them
                foreach (var prefix in _prefixes.Values)
                {
                    // ignore possible failures
                    _urlGroup.UnregisterPrefix(prefix.FullPrefix);
                }
            }
        }
    }
}
