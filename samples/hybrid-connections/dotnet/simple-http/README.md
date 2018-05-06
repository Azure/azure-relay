# 'Simple' HTTP Hybrid Connection Sample 

This sample shows how to use the Hybrid Connections API for HTTP requests from C#.

It consists of a simple command line client and a simple command line server
app. Both applications take the same set of input arguments to launch.

Once you have built the solution, first start the server with 

```dotnet server.dll  [ns] [hc] [keyname] [key]```

and then start the client with 

```dotnet client.dll  [ns] [hc] [keyname] [key]```

whereby the arguments are as follows:

* [ns] - fully qualified domain name for the Service Bus namespace, eg. contoso.servicebus.windows.net
* [hc] - name of a previously configured Hybrid Connection (see [main README](../../README.md))
* [keyname] - name of a SAS rule that confers the required right(s). "Listen" is required for the 
server, "Send" is required for the client. The default management rule confers both.
* [key] - the key for the SAS rules

Once started, the client will connect to the server and send a request.

You can start multiple server instances bound to the same Hybrid Connection to
see the effects of the Relay's load balancing capability, as client connections will be
distributed across existing listeners, up to 25 concurrent.

You can obiously also start multiple server instances bound to the same Hybrid Connection
concurrently on different machines.   

You can create several hundred concurrent client instances if you like. 

## Server 

The server code first creates a new hybrid connection client:

```csharp
var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(keyname, key);
var listener = new HybridConnectionListener(new Uri(String.Format("sb://{0}/{1}", ns, hc)), tokenProvider);
``` 

Then it subscribes to the status events that provide monitoring transparency into the client:

```csharp 
listener.Connecting += (o, e) => { Console.WriteLine("Connecting"); };
listener.Offline += (o, e) => { Console.WriteLine("Offline"); };
listener.Online += (o, e) => { Console.WriteLine("Online"); };
``` 

Then a callback for HTTP request handling is registered:

```csharp
    // Provide an HTTP request handler
    listener.RequestHandler = (context) =>
    {
        // Do something with context.Request.Url, HttpMethod, Headers, InputStream...
        context.Response.StatusCode = HttpStatusCode.OK;
        context.Response.StatusDescription = "OK";
        using (var sw = new StreamWriter(context.Response.OutputStream))
        {
            sw.WriteLine("hello!");
        }
        
        // The context MUST be closed here
        context.Response.Close();
    };
```

Opening the listener will establish the control channel to the Azure Relay
service. The control channel will be continuously maintained and reestablished
when connectivity is disrupted.

```csharp
await listener.OpenAsync(cts.Token);
Console.WriteLine("Server listening");

``` 

The application then waits for a key to be pressed and then closes the server.

```csharp
// Start a new thread that will continuously read the console.
await Console.In.ReadLineAsync();

// Close the listener after you exit the processing loop.
await listener.CloseAsync();
``` 

## Client

If you have disabled the "Requires Client Authorization" option when creating the Relay,
you can send requests to the Hybrid Connections URL with any browser. For accessing
protected endpoints, you need to create and pass a token in the `ServiceBusAuthorization`
header, which is shown here.
.

```csharp

var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(
        KeyName, Key);
var uri = new Uri(string.Format("https://{0}/{1}", RelayNamespace, ConnectionName));
var token = (await tokenProvider.GetTokenAsync(uri.AbsoluteUri, TimeSpan.FromHours(1))).TokenString;
var client = new HttpClient();
var request = new HttpRequestMessage()
{
    RequestUri = uri,
    Method = HttpMethod.Get,
};
request.Headers.Add("ServiceBusAuthorization", token);
var response = await client.SendAsync(request);
Console.WriteLine(await response.Content.ReadAsStringAsync());

``` 

