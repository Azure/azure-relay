# Azure Service Bus Relay HTTP Sample

This sample shows how to create and run a simple ("REST") relayed HTTP(S) service that doesn't require the 
client to present an authorization token for the Relay and is therefore fully transparent. The sample
is a variation of the [RelayHttp](../RelayHttp) sample.

It also demonstrates the Relay's load balancing capabilities.   

## Prerequisites and Setup

All samples share the same basic setup, explained in the main [README](../README.md) file. There are no extra setup steps for this sample.
The application entry points are in [Main.cs](../common/Main.md), which is shared across all samples. The sample implementations generally 
reside in *Program.cs*, starting with Run().    

You can build the sample from the command line with the [build.bat](build.bat) or [build.ps1](build.ps1) scripts. This assumes that you 
have the .NET Build tools in the path. You can also open up the [RelayHttpNoAuth.sln](RelayHttpNoAuth.sln) solution file with Visual Studio and build.
With either option, the NuGet package manager should download and install the **WindowsAzure.ServiceBus** package containing the 
Microsoft.ServiceBus.dll assembly, including dependencies.     

## Service

The implementation is in a single file [Program.cs](./service/Program.cs) and implements a minimal Windows Communication Foundation (WCF)
HTTP web service. 

The host is creates as a <code>WebServiceHost</code>, which takes care of all the right incantations for a WCF web service to function. Instead of
the stock WCF WebHttpBinding, this sample uses the [WebHttpRelayBinding](https://msdn.microsoft.com/library/azure/microsoft.servicebus.webhttprelaybinding.aspx) that comes with the Service Bus client assembly.

We configure the binding with the [EndToEndWebHttpSecurityMode.None](https://msdn.microsoft.com/library/microsoft.servicebus.endtoendwebhttpsecuritymode.aspx) option, which permits plain HTTP access at the Relay.

We also define the [RelayClientAuthenticationType.None](https://msdn.microsoft.com/library/microsoft.servicebus.relayclientauthenticationtype.aspx) option allowing anonymous access to the Service Bus hosted endpoint.  

```csharp
using (var host = new WebServiceHost(GetType()))
{
    host.AddServiceEndpoint(
        GetType(),
        new WebHttpRelayBinding(
            EndToEndWebHttpSecurityMode.None,
            RelayClientAuthenticationType.None) {IsDynamic = false},
        httpAddress)
        .EndpointBehaviors.Add(
            new TransportClientEndpointBehavior(
                TokenProvider.CreateSharedAccessSignatureTokenProvider(listenToken)));

     host.Open();
```

Exactly like the base sample, the web service is a simple GET on the sub-path './Image' that returns a JPEG image

```csharp
        [OperationContract, WebGet]
        Stream Image()
        {
            var stream = new MemoryStream();
            SampleImage.Save(stream, ImageFormat.Jpeg);
            stream.Position = 0;
            WebOperationContext.Current.OutgoingResponse.ContentType = "image/jpeg";
            return stream;
        }
```

## Client

There is no dedicated client for this sample since it enables plain HTTP access. Thus, the server sample
simply pops up the default browser on the local machine to hit the endpoint.

##Running the sample

You can run the service from Visual Studio or on the command line from the sample's root directory by starting <code>start service/bin/debug/service.exe</code> 
