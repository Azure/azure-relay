# Azure Relay Hybrid Connections - Apache Thrift Listener

This sample shows how Hybrid Connections can be used to host an RPC model of 
your choice. This sample is not an endorsement of Apache Thrift.

[Apache Thrift](https://thrift.apache.org/) "software framework, for scalable
cross-language services development, combines a software stack with a code
generation engine to build services that work efficiently and seamlessly between
C++, Java, Python, PHP, Ruby, Erlang, Perl, Haskell, C#, Cocoa, JavaScript,
Node.js, Smalltalk, OCaml and Delphi and other languages."

# Apache Thrift

Thrift is a serialization and RPC framework that generates code from metadata,
and yields fairly efficient encoding on the wire. This sample, as checked in, is
based on the stable version **0.9.2**. You will have to regenerate the files in
the "gen-csharp" directories in Client and Server for any other version of
Thrift, since generated code depends directly on the library version.

The client and server files are slightly modified versions from those found in
the [official C# Thrift tutorial](https://thrift.apache.org/tutorial/csharp),
and the metadata files [tutorial.thrift](tutorial.thrift) and
[shared.thrift](shared.thrift) are taken directly from the tutorial. For that reason, 
the code is also not taking advantage of the asynchronous programming model and 
using blocking operations when required. 

The files in the [client's](client/gen-sharp) and [server's](server/gen-sharp)
directories have been generated, as prescribed by the tutorial using [the Thrift
0.9.2
compiler](http://www.apache.org/dyn/closer.cgi?path=/thrift/0.9.2/thrift-0.9.2.exe)

<code>thrift -r --gen csharp tutorial.thrift</code>



## Client

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
also part of Thrift and readily composes with the *HybridConnectionStream* that the 
*HybridConnectionClient* yields:

```csharp
var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(keyname, key);
var sendAddress = new Uri(string.Format("sb://{0}/{1}", ns, hc));
var relayClient = new HybridConnectionClient(sendAddress, tokenProvider);
var relayConnection = relayClient.CreateConnectionAsync().GetAwaiter().GetResult();

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

We replace that with our own class HybridConnectionListenerServerTransport

```csharp           
CalculatorHandler handler = new CalculatorHandler();
Calculator.Processor processor = new Calculator.Processor(handler);

TServerTransport serverTransport = new HybridConnectionListenerServerTransport(
    new HybridConnectionListener(listenAddress,tokenProvider));

TServer server = new TSimpleServer(processor, serverTransport);
```

whereby the implementation of that class is just a simple shim around HybridConnectionListener

```csharp
public class HybridConnectionListenerServerTransport : TServerTransport
{
    readonly HybridConnectionListener listener;

    public HybridConnectionListenerServerTransport(HybridConnectionListener listener)
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
        var acceptedConnection = listener.AcceptConnectionAsync().GetAwaiter().GetResult();
            return new TStreamTransport(acceptedConnection, acceptedConnection);
    }
}
```

## How to run

Client and server accept four positional command line parameters in this order:
1. Fully qualified namespace name, e.g. 'myns.servicebus.windows.net'
2. Name of the Hybrid Connection to use, e.g. 'hyco'
3. Name of a SAS rule with listen and send permission, e.g. 'RootManageSharedAccessKey'
4. Key value for the chosen SAS rule

Example:

```bash
server.exe myns.servicebus.windows.net hyco RootManageSharedAccessKey fNuEFnLHvSklCfSiSbrbd3bliTJbfi6dhbP2tMsnWSs=
```

