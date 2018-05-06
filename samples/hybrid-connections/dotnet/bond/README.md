# Bond.Comm Example

This folder contains shows how to implement the [Bond Communications](https://microsoft.github.io/bond/manual/bond_comm.html) RPC protocol on top of Hybrid Connections. 

The Relay.Bond.Epoxy project is a standalone and reusable implementation of the [Bond Epoxy](https://microsoft.github.io/bond/manual/bond_comm_epoxy.html) TCP transport that uses the Azure Relay service 
instead of plain TCP.

Alongside the alternate transport, there are two samples included here that have been adapted from 
the Bond Comm sample set:

* _pingpong_ - a simple request/response service
* _notifyevent_ - a one-way notifcation service

The key difference between these samples and the base samples is that they use the new 
transport, so instead of the ```EpoxyTransportBuilder``` (and related classes) they use 
the ```RelayExpoxyTransportBuilder``` and equivalent "Relay"-prefixed classes.

```csharp
var transport = new RelayEpoxyTransportBuilder(tokenProvider)
    .SetLogSink(new ConsoleLogger())
    .Construct();

var pingPongService = new PingPongService();
RelayEpoxyListener pingPongListener = transport.MakeListener(address);
pingPongListener.AddService(pingPongService);
```

The tokenProvider for the underlying ```HybridConnectionClient``` and ```HybridConnectionListener```
is passed into the ```RelayEpoxyTransportBuilder```. Instead of IP addresses (or hostnames) and ports,
addresses are Relay URIs for Hybrid Connections.

## How to run

Both samples accept four positional command line parameters in this order:
1. Fully qualified namespace name, e.g. 'myns.servicebus.windows.net'
2. Name of the Hybrid Connection to use, e.g. 'hyco'
3. Name of a SAS rule with listen and send permission, e.g. 'RootManageSharedAccessKey'
4. Key value for the chosen SAS rule

Example:

```bash
pingpong.exe myns.servicebus.windows.net hyco RootManageSharedAccessKey fNuEFnLHvSklCfSiSbrbd3bliTJbfi6dhbP2tMsnWSs=
```


