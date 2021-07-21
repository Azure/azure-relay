# RoleBasedAccessControl WCF-Relays Sample

This sample shows how to create a WCF Relay Sender and a WCF Relay Listener instances with Managed Identities and AAD RBAC Authentication methods. It takes a set of input arguments to launch

You can run this application from command line using any of three RBAC authentication methods: Please refer to the below individual sections for more details
- **System Assigned Identity:**

   ```RoleBasedAccessControl.exe [HostAddress] [WCFRelayName]```

- **User Assigned Identity:**
  
  ```RoleBasedAccessControl.exe [HostAddress] [WCFRelayName] [ClientId]```

- **AAD:**
  
  ```RoleBasedAccessControl.exe [HostAddress] [WCFRelayName] [ClientId] [TenantId] [ClientSecret]```

whereby the arguments are as follows:

* [HostAddress] - Fully qualified domain name for the Azure Relay namespace, eg. sb://contoso.servicebus.windows.net
* [WCFRelayName] - Name of a WCF Relay
* [ClientId] - Not applicable to System Assigned Identity
*   - For User Assigned Identity, the ClientId associated with a User Assigned Identity resource.
*   - For AAD, the Application (Client) ID associated with an AAD registered application
* [TenantId] - Directory (Tenant) ID associated with an AAD registered application. Only applicable to AAD Rbac.
* [ClientSecret] - Secret Value of a Client Secret associated with an AAD registered application. Only applicable to AAD Rbac.

Once started, a WCF Relay Sender and a WCF Relay listener would be created with the specified RBAC Authentication method

## System Assigned Identity

To run an application within an Azure resource with an enabled System Assigned Identity, please run the executable with the following arguments:

```RoleBasedAccessControl.exe [HostAddress] [WCFRelayName]```

For example:

```RoleBasedAccessControl.exe sb://contoso.servicebus.windows.net myWCF```

The below code creates and returns a TokenProvider object for a System Assigned Identity use case
```csharp
static TokenProvider GetManagedIdentityTokenProvider()
{
    return TokenProvider.CreateManagedIdentityTokenProvider(new Uri("https://relay.azure.net/"));
}
```

## User Assigned Identity

To run an application with an Azure resource asociated with an User Assigned Identity, please run the executable with the following arguments: ClientId is associated with your User Assigned Identity resource

```RoleBasedAccessControl.exe [HostAddress] [WCFRelayName] [ClientId]```

For example:

```RoleBasedAccessControl.exe sb://contoso.servicebus.windows.net myWCF <YourClientId>```

The below code creates and returns a TokenProvider object for an User Assigned Identity use case

```csharp
static TokenProvider GetUserAssignedIdentityTokenProvider(string clientId)
{
    var azureServiceTokenProvider = new AzureServiceTokenProvider($"RunAs=App;AppId={clientId}");
    return TokenProvider.CreateManagedIdentityTokenProvider(azureServiceTokenProvider, new Uri("https://relay.azure.net/"));
}
```

## AAD

To run an AAD registered application, please run the executable with the following arguments: Application (Client) ID , Directory (Tenant) ID is associated with your registered AAD application. ClientSecreat is the Secret Value of a Client Secret associated with your AAD registered application 

```RoleBasedAccessControl.exe [HostAddress] [WCFRelayName] [ClientId] [TenantId] [ClientSecret]```

For example:

```RoleBasedAccessControl.exe sb://contoso.servicebus.windows.net myWCF <YourClientId> <YourTenantId> <YourClientSecret>```

The below code creates and returns a TokenProvider object for an User Assigned Identity use case

```csharp
static TokenProvider GetAadTokenProvider(string clientId, string tenantId, string clientSecret)
{
    return TokenProvider.CreateAzureActiveDirectoryTokenProvider(
        async (audience, authority, state) =>
        {
            IConfidentialClientApplication app = ConfidentialClientApplicationBuilder.Create(clientId)
                        .WithAuthority(authority)
                        .WithClientSecret(clientSecret)
                        .Build();

            var authResult = await app.AcquireTokenForClient(new string[] { $"{audience}/.default" }).ExecuteAsync();
            return authResult.AccessToken;
        },
        new Uri("https://relay.azure.net/"),
        $"https://login.microsoftonline.com/{tenantId}");
}
```

## Create a WCF Relay Listener

The below code snippet creates a WCF Relay Listener with the TokenProvider object returned by one of the above methods. Please refer to the Program.cs for more details

```csharp
ServiceEndpoint endpoint = null;
if (binding is NetTcpRelayBinding || binding is BasicHttpRelayBinding || binding is WSHttpRelayBinding)
{
    serviceHost = new ServiceHost(typeof(MathService), relayAddress);
    endpoint = serviceHost.AddServiceEndpoint(typeof(IMathService), binding, string.Empty);
}
else if (binding is WebHttpRelayBinding)
{
    serviceHost = new WebServiceHost(WebHttpService.Instance, relayAddress);
    endpoint = serviceHost.AddServiceEndpoint(typeof(IWebRequestResponse), binding, string.Empty);
    endpoint.Behaviors.Add(new WebHttpBehavior());
}
else if (binding is NetOnewayRelayBinding)
{
    serviceHost = new ServiceHost(new NotificationService(), relayAddress);
    endpoint = serviceHost.AddServiceEndpoint(typeof(INotificationService), binding, string.Empty);
}

endpoint.EndpointBehaviors.Add(new TransportClientEndpointBehavior(tokenProvider));
```

## Create a WCF Relay Sender

The below code snippet creates a WCF Relay sender with the TokenProvider object returned by one of the above methods. Please refer to the Program.cs for more details

```csharp
static void CreateAndSendWithChannel(Uri address, TokenProvider tokenProvider, Binding binding)
{
    if (binding is NetTcpRelayBinding || binding is BasicHttpRelayBinding || binding is WSHttpRelayBinding)
    {
        CreateAndSendWithMathChannel(address, tokenProvider, binding);
    }
    else if (binding is NetOnewayRelayBinding)
    {
        // Includes NetEvent as well
        CreateAndSendWithNotificationChannel(address, tokenProvider, binding);
    }
    else if (binding is WebHttpBinding)
    {
        CreateAndSendWithWebRequestResponseChannel(address, tokenProvider, binding);
    }
}
```
