# Azure Service Bus Relay - Apache Thrift Listener

This sample builds on the [RelayListener](../RelayListener) sample to illustrate
how that abstraction can be used to host an RPC model of your choice. This sample is 
not an endorsement of Apache Thrift. It shows how to leverage the relay listener.

[Apache Thrift](https://thrift.apache.org/) "software framework, for scalable cross-language 
services development, combines a software stack with a code generation engine to build 
services that work efficiently and seamlessly between C++, Java, Python, PHP, Ruby, Erlang, 
Perl, Haskell, C#, Cocoa, JavaScript, Node.js, Smalltalk, OCaml and Delphi and other languages."

Since RelayClient/-Listener are a .NET example, we'll take care of C# first here and then see about the 
other ones in further samples...

#Apache Thrift

Thrift is a serialization and RPC framework that generates code from metadata, and yields 
fairly efficient encoding on the wire. This sample, as checked in, is based on the stable version **0.9.2**. 
You will have to regenerate the filed in the "gen-csharp" directories in Client and Server for
any other version of Thrift, since generated code depends directly on the library version.

The client and server files are slightly modified versions from those found in the 
[official C# Thrift tutorial](https://thrift.apache.org/tutorial/csharp), and the 
metadata files [tutorial.thrift](tutorial.thrift) and [shared.thrift](shared.thrift) 
are taken directly from the tutorial.

The files in the [client's](client/gen-sharp) and [server's](server/gen-sharp) directories
have been generated, as prescribed by the tutorial using [the Thrift 0.9.2 compiler](http://www.apache.org/dyn/closer.cgi?path=/thrift/0.9.2/thrift-0.9.2.exe)

<code>thrift -r --gen csharp tutorial.thrift</code>

 ##Client

The [client program](client/Program.cs) is a direct copy/paste adaptation of the client 
from the Thirft tutorial, modified for our [shared entry point](../common/Main.md) and 
making the following replacement.

The original sample is using a *TSocket* abstraction that is part of Thrift for the client
transport:

```csharp
TTransport transport = new TSocket("localhost", 9090);
TProtocol protocol = new TBinaryProtocol(transport);
Calculator.Client client = new Calculator.Client(protocol);

transport.Open();
```

In our version, we replace the *TSocket* with a *TStreamTransport* wrapper that is 
also part of Thrift and readily composes with the *RelayConnection* that the 
*RelayClient* yields:

```csharp
var relayClient = new RelayClient(sendAddress, 
                        TokenProvider.CreateSharedAccessSignatureTokenProvider(sendToken));
var relayConnection = relayClient.Connect();

TTransport transport = new TStreamTransport(relayConnection, relayConnection);
TProtocol protocol = new TBinaryProtocol(transport);
Calculator.Client client = new Calculator.Client(protocol);

transport.Open();
```

## Server

The [server program](server/Program.cs) is also a direct adaptation of the tutorial
with a few modifications. The original sets up a *TServerSocket* transport:

```csharp
CalculatorHandler handler = new CalculatorHandler();
Calculator.Processor processor = new Calculator.Processor(handler);
TServerTransport serverTransport = new TServerSocket(9090);
TServer server = new TSimpleServer(processor, serverTransport);
```

We replace that with our own class RelayListenerServerTransport

```csharp           
CalculatorHandler handler = new CalculatorHandler();
Calculator.Processor processor = new Calculator.Processor(handler);

TServerTransport serverTransport = new RelayListenerServerTransport(
    new RelayListener(listenAddress,
    TokenProvider.CreateSharedAccessSignatureTokenProvider(listenToken),
    RelayAddressType.Configured));

TServer server = new TSimpleServer(processor, serverTransport);
```

whereby the implementation of that class is just a simple shim around RelayListener

```csharp
public class RelayListenerServerTransport : TServerTransport
{
    readonly RelayListener listener;

    public RelayListenerServerTransport(RelayListener listener)
    {
        this.listener = listener;
    }

    public override void Listen()
    {
        this.listener.StartAsync().GetAwaiter().GetResult();
    }

    public override void Close()
    {
        this.listener.Stop();
    }

    protected override TTransport AcceptImpl()
    {
        var acceptedConnection = this.listener.AcceptConnection(TimeSpan.MaxValue);
        return new TStreamTransport(acceptedConnection, acceptedConnection);
    }
}
```

## Running the sample

You can run the client from Visual Studio or on the command line from the sample's root directory by starting <code>client/bin/debug/client.exe</code>. You
must run the service from Visual Studio or the command line, preferably with <code>start server/bin/debug/server.exe</code> into a separate, parallel command window, and the service must report itself as listening
before you can start the client.
