# Azure Relay WCF Relay Samples

This repository contains the official set of samples for the Azure Relay service. 

Fork and play!

The *Azure Relay* is a service that helps creating secured, publicly discoverable and reachable endpoints for 
privately hosted services that reside behind network address translation boundaries and/or are shielded by
firewalls. 

In other words, you can host a publicly accessible service endpoint from nearly any Windows computer 
that can connect to the Internet. 

The Relay service client automatically tunnels through proxies, and will automatically 
leverage the [WebSocket protocol](https://tools.ietf.org/html/rfc6455) when and if required.  

The Relay offers two protocol options:
* **HTTP** - HTTP services are the most interoperable option and allow accessing such relayed services from
    any platform. HTTP services can be plain REST-style services, or can use the full range of SOAP/WS-* 
	capabilities of WCF, including message-level end-to-end message protection and authentication.   
* **TCP** - TCP (or "net.tcp") services use a .NET specific, bi-directional, connection-oriented protocol
    that is significantly more efficient than HTTP, and specifically so with the Azure Service Bus Relay. Unless
	interoperability with non-.NET clients is an immediate concern, applications should prefer this option.
	It's possible to host multiple concurrent endpoints for the same service to provide an optimal choice for
	.NET clients and other clients at the same time.   

The ability to create listeners is currently limited to clients running the full .NET Framework (4.5+) on 
supported Windows client (Windows 7+) and Windows Server (Windows Server 2012+) operating systems since
the listener infrastructure is integrated with and leverages the Windows Communication Foundation (WCF).

As the samples will show you, many core Relay scenarios do not require much, if any, prior experience with creating WCF 
services or dealing with the occasionally scary depths of WCF configuration files.       

## Requirements and Setup

These samples run against the cloud service and require that you have an active Azure subscription available 
for use. If you do not have a subscription, [sign up for a free trial](https://azure.microsoft.com/pricing/free-trial/), 
which will give you **ample** credit to experiment with the Relay. 
  
Most Relay samples, except for the ones in the *RelayClients* folder, assume that you are running on a supported 
Windows version and have a .NET Framework 4.5+ build environment available. [Visual Studio 2015](https://www.visualstudio.com/) is recommended to 
explore the samples; the free community edition will work just fine.    

To run the samples, you must perform a few setup steps, including creating and configuring a Service Bus Namespace. 
For the required [setup.ps1](setup.ps1) and [cleanup.ps1](cleanup.ps1) scripts, **you must have Azure Powershell installed** 
([if you don't here's how](https://azure.microsoft.com/en-us/documentation/articles/powershell-install-configure/)) and 
run these scripts from the Azure Powershell environment.

### Setup      
The [setup.ps1](setup.ps1) script will either use the account and subscription you have previously configured for your Azure Powershell environment
or prompt you to log in and, if you have multiple subscriptions associated wiuth your account, select a subscription. 

The script will then create a new Azure Service Bus Namespace for running the samples and configure it with shared access signature (SAS) rules
granting send, listen, and management access to the new namespace. The configuration settings are stored in the file "azure-relay-config.properties", 
which is placed into the user profile directory on your machine. All samples use the same [entry-point boilerplate code](common/Main.cs) that 
retrieves the settings from this file and then launches the sample code. The upside of this approach is that you will never have live credentials 
left in configuration files or in code that you might accidentally check in when you fork this repository and experiment with it.   

### Cleanup

The [cleanup.ps1](cleanup.ps1) script removes the created Service Bus Namespace and deletes the "azure-relay-config.properties" file from 
your user profile directory.
 
## Common Considerations

Most samples use shared [entry-point boilerplate code](common/Main.cs) that loads the configuration and then launches the sample's 
**Program.Run()** instance methods. 

Except for the samples that explicitly demonstrate security capabilities, all samples are invoked with an externally issued SAS token 
rather than a connection string or a raw SAS key. The security model design of Service Bus generally prefers clients to handle tokens 
rather than keys, because tokens can be constrained to a particular scope and can be issued to expire at a certain time. 
More about SAS and tokens can be found [here](https://azure.microsoft.com/documentation/articles/service-bus-shared-access-signature-authentication/).               

## TCP Introduction Samples

* **RelayTcp** - The [RelayTcp](RelayTcp) sample shows how create a TCP listener, how to securely connect to it from a client, and how to pass messages. 
The sample also demonstrates the inherent load balancing capabilities of Service Bus and illustrates how to force HTTPS and WebSocket connectivity 
in tightly managed network environments 
* **RelayListener** - The [RelayListener](RelayListener) sample provides a simple stream abstraction for the Relay, echoing the System.Net.TcpListener/TcpClient classes
from the .NET framework. No WCF experience required. 

##Other TCP Samples
* **RelayTcpStream** - The [RelayTcpStream](RelayTcpStream) sample shows how to wrap any System.IO.Stream with a service endpoint, 
how to create a System.IO.Stream proxy on the client side, and how to run an end-to-end streamed communication link through the Relay.
* **RelayTcpHybrid** - The [RelayTcpHybrid](RelayTcpHybrid) sample shows how to use the Hybrid connection mode, which starts with a connection through the
Relay and upgrades the connection in-flight to a direct NAT traversal connection or local network connection as applicable to reduce latency.
* **RelayTcpMessageLevelSecurity** - The [RelayTcpMessageLevelSecurity](RelayTcpMessageLevelSecurity) sample illustrates how to use WCF's message
security capabilities for end-to-end protection of a message path through the Relay, which end-to-end payload-level encryption.
* **RelayListenerThrift** - The [RelayListenerThrift](RelayListenerThrift) sample is a variation of the standard Apache Thrift C# RPC sample 
overlaid over the bi-directional streaming provided by the [RelayListener](RelayListener)
  

## HTTP Samples
* **RelayHttp** - The [RelayHttp](RelayHttp) sample shows how to create a plain HTTP listener, and how to securely connect to it using 
a simple HTTP client for accessing a resource. The [Service](RelayHttp/Service) project is also the one used with the *RelayClients* 
sample set. 
* **RelayHttpNoAuth** - The [RelayHttpNoAuth](RelayHttpNoAuth) sample illustrates a plain HTTP listener for which the authorization gate at
the Service Bus Relay has been turned off and that allows unauthenticated requests to pass through the Relay. This enables any client, including
browser clients, to transparently interact with the HTTP service without having to present a Relay access token.

## WCF SOAP Web Services Samples
* **RelayWSHttpSimple** - The [RelayWSHttpSimple](RelayWSHttpSimple) sample shows a WCF HTTP Web Service exposed through the Relay
* **RelayWSMetadataExchange** - The [RelayWSMetadataExchange](RelayWSMetadataExchange) sample shows how to expose MEX endpoints through the Relay

## General Samples
*  **RelayConnnectionStatus** - The [RelayConnectionStatus](RelayConnectionStatus) sample shows how to monitor the status of a Relay listener 
when network failures occur, and how to manage opening the listener host should there be no network connectivity at startup time. 
