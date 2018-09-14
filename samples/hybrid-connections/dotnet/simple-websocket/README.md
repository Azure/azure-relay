# 'Simple' WebSocket Hybrid Connection Sample 

This sample shows how to use the Hybrid Connections API for WebSockets from C#.

It consists of a simple command line client and a simple command line server
app. Both applications take the same set of input arguments to launch.

Once you have built the solution, first start the server with 

```server.exe  [ns] [hc] [keyname] [key]```

and then start the client with 

```client.exe  [ns] [hc] [keyname] [key]```

whereby the arguments are as follows:

* [ns] - fully qualified domain name for the Azure Relay namespace, eg. contoso.servicebus.windows.net
* [hc] - name of a previously configured Hybrid Connection (see [main README](../../README.md))
* [keyname] - name of a SAS rule that confers the required right(s). "Listen" is required for the 
server, "Send" is required for the client. The default management rule confers both.
* [key] - the key for the SAS rules

Once started, the client will connect to the server. You can then enter lines of text
which will be sent to the server and returned. A blank line closes the connection.

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

Opening the listener will establish the control channel to the Azure Relay
service. The control channel will be continuously maintained and reestablished
when connectivity is disrupted.

```csharp
await listener.OpenAsync(cts.Token);
Console.WriteLine("Server listening");

``` 

The cancellation hook closes the connection when the user presses ENTER on the
server console.

```csharp

cts.Token.Register(() => listener.CloseAsync(CancellationToken.None));
Task.Run(() => Console.In.ReadLineAsync().ContinueWith((s) => { cts.Cancel(); }));

``` 

Then the code enters the accept loop where new incoming connections are being
awaited and then handed off to a parallel task for handling: 

```csharp
do
{
     var relayConnection = await listener.AcceptConnectionAsync();
    if (relayConnection == null)
        break;
```

The task processes a new session. The connection is a fully bidrectional stream
that the code puts a stream reader and a stream writer over. That allows the
code to read UTF-8 text data that comes from the sender and to write text
replies back.

```csharp
    Task.Run(async () =>
    {
        Console.WriteLine("New session");
        
        var reader = new StreamReader(relayConnection);
        var writer = new StreamWriter(relayConnection) { AutoFlush = true };
        do
        {
            string line = await reader.ReadLineAsync();
            if (String.IsNullOrEmpty(line))
            {
```
                
If there's no input data, the handler will signal that it will no longer send data
on this connection and then break out of the processing loop.

```csharp            
                await relayConnection.ShutdownAsync(cts.Token);
                break;
            }
```

Output received line to the console and write it back to the client, prefixed with "Echo:"

```csharp            
            Console.WriteLine(line);
            await writer.WriteLineAsync("Echo: " + line);
        }
        while (!cts.IsCancellationRequested);
        Console.WriteLine("End session");
        await relayConnection.CloseAsync(cts.Token);
    });
}
while (true);

await listener.CloseAsync(cts.Token); 
```

## Client

The client creates a new hybrid connection client and creates a new connection
right away.

```csharp

var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(keyname, key);
var client = new HybridConnectionClient(new Uri(String.Format("sb://{0}/{1}", ns, hc)),tokenProvider);
var relayConnection = await client.CreateConnectionAsync();

``` 

The client runs two concurrent loops on the connection. One reads input from the console
and writes it to the connection with a stream writer. The other reads lines of
input from the connection with a stream reader and writes them to the console.
Entering a blank line will shut down the write task after sending it to the
server. The server will then cleanly shut down the connection and will
terminate the read task.


```csharp
var reads = Task.Run(async () => {
    // initialize the stream reader over the connection
    var reader = new StreamReader(relayConnection);
    var writer = Console.Out;
    do
    {
        // read a full line of UTF-8 text up to newline
        string line = await reader.ReadLineAsync();
        // if the string is empty or null, we are done.
        if (String.IsNullOrEmpty(line))
            break;
        // write to the console
        await writer.WriteLineAsync(line);
    }
    while (true);
});

// read from the console and write to the hybrid connection
var writes = Task.Run(async () => {
    var reader = Console.In;
    var writer = new StreamWriter(relayConnection) { AutoFlush = true };
    do
    {
        // read a line form the console
        string line = await reader.ReadLineAsync();
        // write the line out, also when it's empty
        await writer.WriteLineAsync(line);
        // quit when the line was empty
        if (String.IsNullOrEmpty(line))
            break;
    }
    while (true);
});

// wait for both tasks to complete
await Task.WhenAll(reads, writes);
await relayConnection.CloseAsync(CancellationToken.None);

```


