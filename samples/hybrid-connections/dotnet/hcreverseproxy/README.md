# Hybrid Connections Reverse Proxy

This sample is a simple HTTP reverse proxy server realized with the
Hybrid Connections HTTP support.

The sample consists of the reverse proxy application and a sample
website project. You can also use most other websites.

The reverse proxy is a simple console application that gets invoked
with two parameters: A connection string for the Hybrid Connection
on the Relay, and a target URI to which requests ought to be forwarded.

`dotnet Microsoft.AzureRelay.ReverseProxy.dll {connection-string} {target-uri}` 

## Considerations

Reverse proxies can be tricky business and this code is just a minimal
starting point. The proxied site is projected into the Relay under a
particular address, like `https://mynamespace.servicebus.windows.net/hcname`,
which means that any self-referencing URLs must emit that address. If
the web site or web service is originally served from the site root or
a different path, URLs may also have to rewritten. 

## Code

The core of the sample is `HybridConnectionReverseProxy.cs`.

In the constructor, a new `HybridConnectionListener` is instantiated based
on the given connection string, and an `HttpClient` is created for
passing received requests onward to the target.

```
        public HybridConnectionReverseProxy(string connectionString, Uri targetUri)
        {
            this.listener = new HybridConnectionListener(connectionString);
            this.httpClient = new HttpClient();
            this.httpClient.BaseAddress = targetUri;
            this.httpClient.DefaultRequestHeaders.ExpectContinue = false;
            this.hybridConnectionSubpath = this.listener.Address.AbsolutePath.EnsureEndsWith("/");
        }
```

The `OpenAsync` method sets up the request handler and then starts the listener.

```
        public async Task OpenAsync(CancellationToken cancelToken)
        {
            this.listener.RequestHandler = (context) => this.RequestHandler(context);
            await this.listener.OpenAsync(cancelToken);
            Console.WriteLine($"Forwarding from {this.listener.Address} to {this.httpClient.BaseAddress}.");
            Console.WriteLine("utcTime, request, statusCode, durationMs");
        }
```

`CloseAsync` closes the listener.

```
        public Task CloseAsync(CancellationToken cancelToken)
        {
            return this.listener.CloseAsync(cancelToken);
        }
```

The `RequestHandler` that is registered in `OpenAsync` is invoked whenever a
request has been received on the Relay. The received HTTP request is 
repackaged into a downstream HTTP request and sent to the target. The
response from the target is then repackaged and sent back via the Relay.

```
        async void RequestHandler(RelayedHttpListenerContext context)
        {
            DateTime startTimeUtc = DateTime.UtcNow;
            try
            {
                HttpRequestMessage requestMessage = CreateHttpRequestMessage(context);
                HttpResponseMessage responseMessage = await this.httpClient.SendAsync(requestMessage);
                await SendResponseAsync(context, responseMessage);
                await context.Response.CloseAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.GetType().Name}: {e.Message}");
                SendErrorResponse(e, context);
            }
            finally
            {
                LogRequest(startTimeUtc, context);
            }
        }
```

The `CreateHttpRequestMessage` method repackages the request.

```

        HttpRequestMessage CreateHttpRequestMessage(RelayedHttpListenerContext context)
        {
            var requestMessage = new HttpRequestMessage();
            if (context.Request.HasEntityBody)
            {
                requestMessage.Content = new StreamContent(context.Request.InputStream);
                string contentType = context.Request.Headers[HttpRequestHeader.ContentType];
                if (!string.IsNullOrEmpty(contentType))
                {
                    requestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                }
            }

            string relativePath = context.Request.Url.GetComponents(UriComponents.PathAndQuery, UriFormat.Unescaped);
            relativePath = relativePath.Replace(this.hybridConnectionSubpath, string.Empty, StringComparison.OrdinalIgnoreCase);
            requestMessage.RequestUri = new Uri(relativePath, UriKind.RelativeOrAbsolute);
            requestMessage.Method = new HttpMethod(context.Request.HttpMethod);

            foreach (var headerName in context.Request.Headers.AllKeys)
            {
                if (string.Equals(headerName, "Host", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(headerName, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    // Don't flow these headers here
                    continue;
                }

                requestMessage.Headers.Add(headerName, context.Request.Headers[headerName]);
            }

            return requestMessage;
        }
```

`SendResponseAsync` takes care of the response  packaging and sending details.

```
        async Task SendResponseAsync(RelayedHttpListenerContext context, HttpResponseMessage responseMessage)
        {
            context.Response.StatusCode = responseMessage.StatusCode;
            context.Response.StatusDescription = responseMessage.ReasonPhrase;
            foreach (KeyValuePair<string, IEnumerable<string>> header in responseMessage.Headers)
            {
                if (string.Equals(header.Key, "Transfer-Encoding"))
                {
                    continue;
                }

                context.Response.Headers.Add(header.Key, string.Join(",", header.Value));
            }

            var responseStream = await responseMessage.Content.ReadAsStreamAsync();
            await responseStream.CopyToAsync(context.Response.OutputStream);
        }
```

If an error occurs, make a little error page:

```
        void SendErrorResponse(Exception e, RelayedHttpListenerContext context)
        {
            context.Response.StatusCode = HttpStatusCode.InternalServerError;

#if DEBUG || INCLUDE_ERROR_DETAILS
            context.Response.StatusDescription = $"Internal Server Error: {e.GetType().FullName}: {e.Message}";
#endif
            context.Response.Close();
        }

```


Finally, some logging.

```
        void LogRequest(DateTime startTimeUtc, RelayedHttpListenerContext context)
        {
            DateTime stopTimeUtc = DateTime.UtcNow;
            StringBuilder buffer = new StringBuilder();
            buffer.Append($"{startTimeUtc.ToString("s", CultureInfo.InvariantCulture)}, ");
            buffer.Append($"\"{context.Request.HttpMethod} {context.Request.Url.GetComponents(UriComponents.PathAndQuery, UriFormat.Unescaped)}\", ");
            buffer.Append($"{(int)context.Response.StatusCode}, ");
            buffer.Append($"{(int)stopTimeUtc.Subtract(startTimeUtc).TotalMilliseconds}");
            Console.WriteLine(buffer);
        }
    }
}

```