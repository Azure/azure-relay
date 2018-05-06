# Azure Service Bus Relay HTTP Sample

This sample shows how to create and run a simple ("REST") relayed HTTP(S) service with minimal ceremony.

It also demonstrates the Relay's load balancing capabilities.   

## Prerequisites and Setup

All samples share the same basic setup, explained in the main [README](../README.md) file. There are no extra setup steps for this sample.
The application entry points are in [Main.cs](../common/Main.md), which is shared across all samples. The sample implementations generally 
reside in *Program.cs*, starting with Run().    

You can build the sample from the command line with the [build.bat](build.bat) or [build.ps1](build.ps1) scripts. This assumes that you 
have the .NET Build tools in the path. You can also open up the [RelayTcp.sln](RelayTcp.sln) solution file with Visual Studio and build.
With either option, the NuGet package manager should download and install the **WindowsAzure.ServiceBus** package containing the 
Microsoft.ServiceBus.dll assembly, including dependencies.     

## Service

The service implementation is in a single file [Program.cs](./service/Program.cs) and implements a minimal Windows Communication Foundation (WCF)
HTTP web service. 

The host is creates as a <code>WebServiceHost</code>, which takes care of all the right incantations for a WCF web service to function. Instead of
the stock WCF WebHttpBinding, this sample uses the [WebHttpRelayBinding](https://msdn.microsoft.com/library/azure/microsoft.servicebus.webhttprelaybinding.aspx) that comes with the Service Bus client assembly.

We configure the binding with the [EndToEndWebHttpSecurityMode.Transport](https://msdn.microsoft.com/library/microsoft.servicebus.endtoendwebhttpsecuritymode.aspx) option, which enforces the use of
HTTPS (with SSL/TLS over port 443) by the client, meaning HTTP access (port 80) will not be available on the Relay endpoint.

We also define the [RelayClientAuthenticationType.RelayAccessToken](https://msdn.microsoft.com/library/microsoft.servicebus.relayclientauthenticationtype.aspx) option, which is also the default choice when omitted, 
enforcing that all clients must bring a valid token with "Send" permission for the endpoint in their HTTPS requests to the Service Bus hosted endpoint.  

```csharp
using (var host = new WebServiceHost(this.GetType()))
{
    var webHttpRelayBinding = new WebHttpRelayBinding(EndToEndWebHttpSecurityMode.Transport,
                                                        RelayClientAuthenticationType.RelayAccessToken)
                                                        {IsDynamic = false};
    host.AddServiceEndpoint(this.GetType(),
        webHttpRelayBinding,
        httpAddress)
        .EndpointBehaviors.Add(
            new TransportClientEndpointBehavior(
                TokenProvider.CreateSharedAccessSignatureTokenProvider(listenToken)));
```

The web service is a simple GET on the sub-path './Image' that returns a JPEG image

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

Since using a plain browser client would throw us a little off track for a C# sample due to the access control 
token requirement and the required JavaScript mechanics, this starter sample comes with a minimal Windows Forms client. 

When loading, the Windows Forms client calls the web service, retrieves the image and displays it.

The only "special" code here is the addition of a header into the HTTP request that contains the supplied SAS token.
Service Bus understands (and strips!) either the <code>Authorization</code> header or the <code>ServiceBusAuthorization</code> header. If the latter is present,
the <code>Authorization</code> header remains uninspected and untouched. 

The token that is targeted at Service Bus is stripped from the request since leaving it in the request would potentially result in 
an elevation of privilege for the listener, coming in possession of a token that also permits sending.

If the application wishes to use end-to-end authorization token flow, it is suggested to use the special <code>ServiceBusAuthorization</code> header as
shown here, which frees up the <code>Authorization</code> header for end-to-end flow. Service Bus primarily supports the <code>Authorization</code> header 
directly for cases where the HTTP client library or framework does not provide the ability to add custom headers.

```csharp
protected override void OnLoad(EventArgs e)
{
    var client = new HttpClient();
    client.DefaultRequestHeaders.Add("ServiceBusAuthorization", this.sendToken);
    var image = client.GetStreamAsync(this.sendAddress + "/image").GetAwaiter().GetResult();
    this.pictureBox.Image = Image.FromStream(image);
}
```

## Running the sample

You can run the client from Visual Studio or on the command line from the sample's root directory by starting <code>client/bin/debug/client.exe</code>. You
must run the service from Visual Studio or the command line, preferably with <code>start service/bin/debug/service.exe</code> into a separate, parallel command window, and the service must report itself as listening
before you can start the client. You can start the service multiple times to see how load balancing works.
