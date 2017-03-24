# Azure Service Bus Relay Connection Status Sample

This sample builds on the [RelayTcp](../RelayTcp) sample and is functionally identical. We're therefore 
not going to discuss the basics here and focus on the delta.

It demonstrates the use of the ConnectionStatusBehavior for monitoring the connection status of the 
listener as well as a strategy to start the listener initially even under adverse networking conditions.

## Prerequisites and Setup

All samples share the same basic setup, explained in the main [README](../README.md) file. There are no extra setup steps for this sample.
The application entry points are in [Main.cs](../common/Main.md), which is shared across all samples. The sample implementations generally 
reside in *Program.cs*, starting with Run().    

You can build the sample from the command line with the [build.bat](build.bat) or [build.ps1](build.ps1) scripts. This assumes that you 
have the .NET Build tools in the path. You can also open up the [RelayConnectionStatus.sln](RelayConnectionStatus.sln) solution file with Visual Studio and build.
With either option, the NuGet package manager should download and install the **WindowsAzure.ServiceBus** package containing the 
Microsoft.ServiceBus.dll assembly, including dependencies.     

##RelayServiceHostController 

The class we'll look at in this sample is the [RelayServiceHostController](service/RelayServiceHistController.cs) that resides in the
[Service](service) project. This class implements a container for the ServiceHost that will aim to ensure that the ServiceHost will 
eventually open even if current network conditions don't allow it, and when the ServiceHost is open, it will provide insight 
into the connection status.

The Service Bus listener is already very robust against network irregularities and will aggressively reconnect as it detects 
network connectivity interruptions, so once the Service Host is once opened, the ConnectionStatusBehavior provides insight 
into the listener's connstation status and recovery actions.

The class we show here helps with the network being unavailable or Service bus not being reachable just as the Service Bus host 
is attempting being opened. While waiting for network connectivity to improve, the codes uses an exponential backoff retry strategy,
leveraging the *Microsoft.Practices.TransientFaultHandling.Core* package.

The controller is constructed providing a callback that yields a new ServiceHost instance. This, rather than passing an initialized 
ServiceHost, is required since a faulted ServiceHost is not recoverable. If the host fails opening, you have to build a new one
and that is the job of that callback.

```csharp
public RelayServiceHostController(Func<ServiceHost> createServiceHost)
{
    this.createServiceHost = createServiceHost;
    // Use exponential backoff in case of failures. Retry forever
    retryStrategy = new ExponentialBackoff(100000, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60),
        TimeSpan.FromSeconds(1));
}
``` 

In the service implementation, you'll see the constructor invoked with a lambda:

```csharp
 var controller = new RelayServiceHostController(() =>
{
    ServiceHost host = new ServiceHost(this);
    host.AddServiceEndpoint(
        GetType(),
        new NetTcpRelayBinding {IsDynamic = false},
        listenAddress)
        .EndpointBehaviors.Add(
            new TransportClientEndpointBehavior(
                TokenProvider.CreateSharedAccessSignatureTokenProvider(
                  listenToken)));
    return host;
});
controller.Open();
```

The setup of all error handling occurs in the controller's <code>Open()</code> method.

```csharp
 public void Open()
{
    this.serviceHost = createServiceHost();
```

After the service host is created, we create a new ConnectionStatusBehavior, wire up the 
callbacks into this class, and then apply the behavior to all endpoints.
```csharp
    statusBehavior = new ConnectionStatusBehavior();
    statusBehavior.Online += ConnectionStatusOnline;
    statusBehavior.Offline += ConnectionStatusOffline;
    statusBehavior.Connecting += ConnectionStatusConnecting;
    // add the Service Bus credentials to all endpoints specified in configuration
    foreach (var endpoint in serviceHost.Description.Endpoints)
    {
        endpoint.Behaviors.Add(statusBehavior);
    }
```

Then we're hooking up a handler to the Faulted event for the ServiceHost, which will 
be triggered when the ServiceHost fails to open. The ServiceBus listener will not 
otherwise cause the ServiceHost to fault while open.

```csharp
    serviceHost.Faulted += ServiceHostFaulted;
```

Then the service host is openend.

```csharp
    // Start the service
    // if Open throws any Exceptions the faulted event will be raised
    try
    {
        serviceHost.Open();
    }
    catch (Exception e)
    {
        // This is handled by the fault handler ServiceHostFaulted
        Console.WriteLine("Encountered exception \"{0}\"", e.Message);
    }
}
```

The fault handler will determine whether the error is a recoverable one and 
then wait per backoff strategy and retry:

```csharp
void ServiceHostFaulted(object sender, EventArgs e)
{
    Console.WriteLine("Relay Echo ServiceHost faulted. {0}", statusBehavior.LastError?.Message);
    serviceHost.Abort();
    serviceHost.Faulted -= ServiceHostFaulted;
    serviceHost = null;

    var isPermanentError = statusBehavior.LastError is RelayNotFoundException
                            || statusBehavior.LastError is AuthorizationFailedException
                            || statusBehavior.LastError is AddressAlreadyInUseException;

    // RelayNotFound indicates the Relay Service could not find an endpoint with the name/address used by this client
    // AuthorizationFailed indicates that the SAS/Shared Secret key used by this client is invalid and needs to be updated
    // AddressAlreadyInUseException indicates that this endpoint is in use with incompatible settings (like volatile vs persistent etc)
    // If we encounter one of these conditions, retrying will not change the results. Operator/Admin intervention is required.

    // Further we believe that in all other cases, the ServiceBus Client itself would retry instead of faulting the ServiceHost
    // However, in case there are bugs that we are not currently aware of, restarting the ServiceHost here may overcome such conditions.

    if (!isPermanentError)
    {
        TimeSpan waitPeriod;
        var shouldRetry = retryStrategy.GetShouldRetry();
        if (shouldRetry(retryCount++, statusBehavior.LastError, out waitPeriod))
        {
            Thread.Sleep(waitPeriod);

            Open();
            Console.WriteLine("Relay Echo ServiceHost recreated ");
        }
    }
    else if (statusBehavior.LastError != null)
    {
        Console.WriteLine("Relay Echo Service encountered permanent error {0}", statusBehavior.LastError?.Message);
    }
}
```