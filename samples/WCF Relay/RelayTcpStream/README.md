# Azure Service Bus Relay TCP Stream Sample

This sample shows how you can bridge bi-directional or unidirectional streams via the Azure Service Bus Relay.

## Prerequisites and Setup

All samples share the same basic setup, explained in the main [README](../README.md) file. There are no extra setup steps for this sample.
The application entry points are in [Main.cs](../common/Main.md), which is shared across all samples. The sample implementations generally 
reside in *Program.cs*, starting with Run().    

You can build the sample from the command line with the [build.bat](build.bat) or [build.ps1](build.ps1) scripts. This assumes that you 
have the .NET Build tools in the path. You can also open up the [RelayTcpStream.sln](RelayTcpStream.sln) solution file with Visual Studio and build.
With either option, the NuGet package manager should download and install the **WindowsAzure.ServiceBus** package containing the 
Microsoft.ServiceBus.dll assembly, including dependencies.     

## Service

The service implementation is all in a single file [Program.cs](Service/Program.cs) with no dependencies on external configuration.
 
The program references *System.ServiceModel* for WCF and *Microsoft.ServiceBus* from the Azure Service Bus NuGet package. 

// 

You can run the service from Visual Studio or on the command line from the sample's root directory by starting "service/bin/debug/service.exe". You
must run the service and the service must report itself as listening before you can start the client.   

You can start multiple instances of the service, and you can also move the configuration to a different machine and run multiple 
instances across different machines. You will observe that multiple listeners can share the same name on the Relay and that the 
Relay will randomly balance sessions across the connected listeners.     

## Client
        
Just like the service, the client implementation is all in a single file [Program.cs](Client/Program.cs) with no dependencies on external configuration.
It also references the same namespaces and packages.

//
 
You can run the client from Visual Studio or on the command line from the sample's root directory by starting "client/bin/debug/client.exe". You
must run the service and the service must report itself as listening before you can start the client.

If you run multiple listeners, every client instance will end up being associated with one of the listeners, as the load balancing feature 
workse at the session level and not at the request level. The next client instance may then get assigned to another service instance.      
 
 ## Where do I go from here?
 
 The [RelayTcpStream](../RelayTcpStream/README.md) sample is very similar to this one, and illustrates how to proxy a stream between a 
 client and a service.  