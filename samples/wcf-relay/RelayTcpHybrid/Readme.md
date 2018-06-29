# Azure Service Bus Relay Hybrid Tcp Sample

This sample shows how to create and run a simple relayed TCP service using the Hybrid connection mode, which starts with a 
connection through the Relay and upgrades the connection in-flight to a direct NAT traversal 
connection or local network connection as applicable to reduce latency.

It is a variant of the [RelayTcp](../RelayTcp) sample, so we're only going to discuss the specific difference in this document.

## Prerequisites and Setup

All samples share the same basic setup, explained in the main [README](../README.md) file. There are no extra setup steps for this sample.
The application entry points are in [Main.cs](../common/Main.md), which is shared across all samples. The sample implementations generally 
reside in *Program.cs*, starting with Run().    

You can build the sample from the command line with the [build.bat](build.bat) or [build.ps1](build.ps1) scripts. This assumes that you 
have the .NET Build tools in the path. You can also open up the [RelayTcpHybrid.sln](RelayTcpHybrid.sln) solution file with Visual Studio and build.
With either option, the NuGet package manager should download and install the **WindowsAzure.ServiceBus** package containing the 
Microsoft.ServiceBus.dll assembly, including dependencies.     

## Service

The service implementation is largely identical to the *RelayTcp* sample, except for one 
key difference. The NettcpRelayBinding is configured differently:

```csharp
var binding = new NetTcpRelayBinding(EndToEndSecurityMode.None, RelayClientAuthenticationType.RelayAccessToken)
{
    ConnectionMode = TcpRelayConnectionMode.Hybrid
};

```

The ConnectionMode property, which defaults to *Relayed* is set to *Hybrid*. This will cause the listener to 
enter a special mode that starts each connection through the Relay, but then attempts to migrate the connection
away from the Relay to a shorter connection path, if possible. 

This "Direct" mode will probe two different paths:
* Client and server will first probe whether they reside on the same local network. For that, the listener will open up 
a local network listener, which als explains why you will potentially see, unlike with any other Azure Service Bus Relay listener, a
prompt from the local firewall for permission on the first run attempt (which may disturb the upgrade attempt).
* In parallel, client and server will probe whether they can establish a NAT traversal path through the Internet, with
assistance from the Azure Service Bus Relay. The algorithm uses a NAT port prediction method and allows for a socket to be 
connected from two coordinated and near simultaneous outbound connection attempts made towards the respective other party.
The chances of the NAT traversal algorithm succeeding depend on the predictability of the NAT and on how busy the NAT is. 
With consumer NAT devices in common household environments, the chance of the algorithm succeeding can exceed 95%.

If the "Direct" mode can be established, the communication path is moved from the relayed socket to the direct socket 
by draining the remaining pending traffic from the relayed connection and resuming the communication on the direct 
socket. The upgrade requires active traffic to proceed and cannot occur on a "quiet" connection.

> <span style="color:red">Security Note</span>
> The Hybrid connection mode is not compatible with the "transport" end-to-end security mode. While the relayed
> connection through the Service Bus Relay will continue to be protected with SSL/TLS, the direct socket is a 
> plain TCP link when established. 
> The [RelayListener](../RelayListener) sample can be modified to use the Hybrid mode and the resulting stream 
> connection can be overlaid with an SslStream for end-to-end path protection, which is an example for how to
> protect the resulting link.
> The base functionality doesn't enforce TLS for the direct connection because the primary motivation for the 
> feature is enabling lower latency links. 


## Client
        
The client uses the exact same modified binding as the service.

