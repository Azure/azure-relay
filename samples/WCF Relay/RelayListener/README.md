# Azure Service Bus - Relay Listener Sample

This sample introduces a small utility library, **AzureServiceBus.RelayListener**, which abstracts away the Relay's default 
Windows Communication Foundation (WCF) API surface and replaces it with three simple classes that echoe the .NET Framework's 
TcpClient and TcpListener classes and provide a simple binary stream abstraction. 

* *RelayListener* provides a self hostable listener that accepts *RelayConnection* instances 
* *RelayClient* establishes connections through the Relay and returns them as *RelayConnection* instances
* *RelayConnection* is a read/write implementation of **System.IO.Stream** 

The implementation of the three classes is based on the low-level [WCF channel model API](https://msdn.microsoft.com/library/ms729840.aspx), 
which does not use typed proxies, serialization, or the service hosting framework and is therefore lighter weight than the full
WCF model.

The resulting links use the .NET binary framing protocol through the Relay, and all payload data is carried byte-for-byte without extra encoding
overhead inside .NET binary format (see [MC-NBFX](https://msdn.microsoft.com/library/cc219210.aspx), [MC-NBFS](https://msdn.microsoft.com/library/cc219175.aspx)) messages. 
While the message information model leans on XML, the encoding is pure binary and eliminates nearly all structural overhead that exists in 
textual representations of XML and leverages dictionary-based compression. The resulting transfer overhead including framing is some 
30 bytes per message, less than 0.2% overhead with the 16384 byte chunks sent by this sample. 

The RelayConnection stream abstraction writes a message for each write operation on its API. For very chatty writers, it's therefore 
advisable to wrap the stream with a [BufferedStream](https://msdn.microsoft.com/library/system.io.bufferedstream.aspx) to reduce the 
number of network writes. 


## RelayListener

The [RelayListener](./AzureServiceBus.RelayListener/RelayListener.cs) class is constructed providing a Relay address, a token 
provider that can dispense a "Listen" token for that address, and a *RelayAddressType*. *RelayAddressType* is an enumeration with the 
values *Configured* and *Dynamic*. Configured endpoints have been created previously with *NamespaceManager.CreateRelayAsync*

```csharp
public RelayListener(string address, TokenProvider tokenProvider, RelayAddressType relayAddressType)
```

The listener is started with <code>StartAsync()</code> and stopped with <code>Stop()</code>. The class is disposable and stops 
when disposed. Accepting new connections is usually done in a loop that calls the <code>AcceptConnectionAsync()</code> method and
then hands off processing to a per-connection loop that drives the processing of the incoming stream. 

This is all put together in the Server project in [Program.cs](./server/Program.cs). The listener is created, 
started, and then the program enters a loop where the program accepts new connections (which are just bi-directional streams) 
and hands them to the *ProcessConnection* method, which simply reads and writes 1MByte of random data in this example. 

The *maxConcurrentConnections* semaphore limits the number of connections that can be handled concurrently.

```csharp
readonly SemaphoreSlim maxConcurrentConnections = new SemaphoreSlim(5);

using (var relayListener = new RelayListener(listenAddress, 
                                  TokenProvider.CreateSharedAccessSignatureTokenProvider(listenToken), 
                                  RelayAddressType.Configured))
{
    await relayListener.StartAsync();
    RelayConnection connection;
    do
    {
        await maxConcurrentConnections.WaitAsync();
        try
        {
            connection = await relayListener.AcceptConnectionAsync(TimeSpan.MaxValue);
        }
        catch
        {
            maxConcurrentConnections.Release();
            throw;
        }
        if (connection != null)
        {
            // not awaiting, we're handling these in parallel on the I/O thread pool
            // and simply running into the first await inside ProcessConnection
            // is sufficient to get the handling off this thread
            ProcessConnection(connection).ContinueWith(
                t => maxConcurrentConnections.Release());
        }
        else
        {
            maxConcurrentConnections.Release();
        }

    } while (connection != null);
}
```

## RelayClient 

The [RelayClient](./AzureServiceBus.RelayListener/RelayClient.cs) is constructed providing the Relay address of an existing 
listener and a token provider that can dispense a "Send" token for that address.

```csharp
 public RelayClient(string address, TokenProvider tokenProvider)
```

The client is effectively a stateless connection factory, and new connections can be created calling <code>ConnectAsync</code>.

The client project in [Program.cs](./client/Program.cs) puts this together:

```csharp
var client = new RelayClient(sendAddress, TokenProvider.CreateSharedAccessSignatureTokenProvider(sendToken));
var connection = await client.ConnectAsync();

await SendAndReceive(connection);

connection.Close();
```

## RelayConnection

The [RelayConnection](./AzureServiceBus.RelayListener/RelayConnection.cs) is a simple stream abstraction that sends and receives 
all messages. Read and write operations can occur concurrently on parallel threads as both the client and the server 
implementations show.

The implementation of <code>ReadAsync</code> may take a moment longer to read through, as it needs to implement read operations spanning 
the payload streams obtained from sequentially fetched messages. Since this requires synchronizing access of the "current" memory 
stream and the network channel, the read operation is guarded by a semaphore. 

## Running the sample

You can run the client from Visual Studio or on the command line from the sample's root directory by starting <code>client/bin/debug/client.exe</code>. You
must run the service from Visual Studio or the command line, preferably with <code>start server/bin/debug/server.exe</code> into a separate, parallel command 
window, and the server must report itself as listening before you can start the client.