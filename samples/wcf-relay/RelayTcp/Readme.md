# Azure Service Bus Relay TCP Sample

This sample shows how to create and run a simple relayed TCP service with minimal ceremony.

It also demonstrates the Relay's load balancing capabilities as well as the HTTPS Firewall traversal feature.

## Prerequisites and Setup

All samples share the same basic setup, explained in the main [README](../README.md) file. There are no extra setup steps for this sample.
The application entry points are in [Main.cs](../common/Main.md), which is shared across all samples. The sample implementations generally 
reside in *Program.cs*, starting with Run().    

You can build the sample from the command line with the [build.bat](build.bat) or [build.ps1](build.ps1) scripts. This assumes that you 
have the .NET Build tools in the path. You can also open up the [RelayTcp.sln](RelayTcp.sln) solution file with Visual Studio and build.
With either option, the NuGet package manager should download and install the **WindowsAzure.ServiceBus** package containing the 
Microsoft.ServiceBus.dll assembly, including dependencies.     

## Service

The service implementation is all in a single file [Program.cs](Service/Program.cs) with no dependencies on external configuration.
 
The program references *System.ServiceModel* for WCF and *Microsoft.ServiceBus* from the Azure Service Bus NuGet package. 
 
```csharp
    using System;
    using System.ServiceModel;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus;
```   

For compactness, the program class itself implements the listener service. As required for WCF, the class is therefore decorated
with a [ServiceContract] attribute and since we'll be hosting the service on the same instance as a singleton object, we also declare 
that fact in the [ServiceBehavior] attribute.   

```csharp
     [ServiceContract(Namespace = "", Name = "echo"),
     ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]

    class Program : ITcpListenerSample
    {
	    public async Task Run(string listenAddress, string listenToken)
        {
```

The first two lines inside the <code>Run()</code> method illustrates the Firewall traversal mode. The first option,
setting the connectivity mode to "AutoDetect" is the default and would be in effect even if we omitted this line. 
The second, commented-out line shows how to alternatively force the HTTPS WebSockets mode for the Relay. The auto-detect
algorithm will usually spot when the (more efficient) outbound TCP ports 9350-9353 used by the Relay are shut down, but this setting
provides a reliable override. The setting is global to the appdomain as the network setup will be identical across
all endpoints. The same two lines are also in the client portion of this sample. The setting does not need be symmetrically
applied on client and server.

```csharp
            ServiceBusEnvironment.SystemConnectivity.Mode = ConnectivityMode.AutoDetect; // Auto-detect, default
            //ServiceBusEnvironment.SystemConnectivity.Mode = ConnectivityMode.Https; // HTTPS WebSockets
```

To set up the service, we create a WCF ServiceHost and pass a reference to this singleton instance. Then we add the service endpoint
on which we want to listen. The service endpoint uses the *NetTcpRelayBinding*, which is functionally equivalent to the NetTcpBinding in
WCF, but creates a Relay endpoint instead of listening on the local network. Since the sample setup preconfigures the Relay endpoint 
for us, we initialize the binding with *IsDynamic=false*.

The communication direction is outbound-only, meaning the listener does not open up the client to any traffic not coming from the 
Azure Service Bus Relay. 

With the standard sample setup, the public address for the listener will take a form similar to *sb://relaySampleNNNNNNNNN.servicebus.windows.net/relaysamples/nettcp*.

The security token that allows the listener to communicate with the Azure Service Bus Relay is added to the endpoint using a "behavior". 
This configuration element, in turn, holds a token provider into which we configure the token that has been passed to Run()  

```csharp
            using (ServiceHost host = new ServiceHost(this))
            {
                host.AddServiceEndpoint(GetType(), new NetTcpRelayBinding() { IsDynamic = false }, listenAddress)
                    .EndpointBehaviors.Add(
                        new TransportClientEndpointBehavior(
                            TokenProvider.CreateSharedAccessSignatureTokenProvider(listenToken)));
```

That's already enough to get the service going, so what's left is to open the host and wait for traffic.       

```csharp               
                host.Open();
                Console.WriteLine("Service listening at address {0}", listenAddress);
                Console.WriteLine("Press [Enter] to close the listener and exit.");
                Console.ReadLine();
                host.Close();
            }
        }
```

The service functionality is very simple a just echoes back the input after logging it to the console. As required for WCF, the 
operation is marked with the [OperationContract] attribute. 

 ```csharp
         [OperationContract]
        async Task<string> Echo(string input)
        {
            Console.WriteLine("\tCall received with input \"{0}\"", input);
            return input;
        }
    }
```

You can run the service from Visual Studio or on the command line from the sample's root directory by starting "service/bin/debug/service.exe". You
must run the service and the service must report itself as listening before you can start the client.   

You can start multiple instances of the service, and you can also move the configuration to a different machine and run multiple 
instances across different machines. You will observe that multiple listeners can share the same name on the Relay and that the 
Relay will randomly balance sessions across the connected listeners.     

## Client
        
Just like the service, the client implementation is all in a single file [Program.cs](Client/Program.cs) with no dependencies on external configuration.
It also references the same namespaces and packages.

The client defines a contract for the client proxy that echoes the inline contract defined in the service program. What matters is that 
the namespace and name declarations as well as the name of the method match as they do here.

```csharp
         [ServiceContract(Namespace = "", Name = "echo")]
        interface IClient : IClientChannel
        {
            [OperationContract]
            Task<string> Echo(string input);
        }
```

In the Run() method, we create a channel factory for the proxy contract, again using the *NetTcpRelayBinding* and passing the send address,
which is the same as the service's listen address. Like on the service-side, we add the token configuration through a behavior, which is 
applied to the channel factory on the client.     


```csharp
        public async Task Run(string sendAddress, string sendToken)
        {
            var cf = new ChannelFactory<IClient>(new NetTcpRelayBinding(), sendAddress);
            cf.Endpoint.EndpointBehaviors.Add(
                new TransportClientEndpointBehavior(
                    TokenProvider.CreateSharedAccessSignatureTokenProvider(sendToken)));
```

Now that the channel factory is configured, we can create the channel. The channel will connect when the first method is called an then 
hold on to the session until the channel is closed. In the loop below we are calling Echo 25 times and then exit.  

```csharp
            using (var client = cf.CreateChannel())
            {
                for (int i = 1; i <= 25; i++)
                {
                    var result = await client.Echo(DateTime.UtcNow.ToString());
                    Console.WriteLine("Round {0}, Echo: {1}", i, result);
                }
                client.Close();
            }

            Console.WriteLine("Press [Enter] to exit.");
            Console.ReadLine();
        }
    }
 ```
 
You can run the client from Visual Studio or on the command line from the sample's root directory by starting "client/bin/debug/client.exe". You
must run the service and the service must report itself as listening before you can start the client.

If you run multiple listeners, every client instance will end up being associated with one of the listeners, as the load balancing feature 
works at the session level and not at the request level. The next client instance may then get assigned to another service instance.      
 
 