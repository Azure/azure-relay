# RoleBasedAccessControl Hybrid Connection Sample

This sample shows how to create a Relay Sender and a Relay Listener instances with Managed Identities and AAD RBAC Authentication methods. It takes a set of input arguments to launch

You can run this application from command line using any of three RBAC authentication methods: Please refer to the below individual sections for more details
- **System Assigned Identity:**

   ```RoleBasedAccessControl.exe [HostAddress] [HybridConnectionName]```

- **User Assigned Identity:**
  
  ```RoleBasedAccessControl.exe [HostAddress] [HybridConnectionName] [ClientId]```

- **AAD:**
  
  ```RoleBasedAccessControl.exe [HostAddress] [HybridConnectionName] [ClientId] [TenantId] [ClientSecret]```

whereby the arguments are as follows:

* [HostAddress] - Fully qualified domain name for the Azure Relay namespace, eg. sb://contoso.servicebus.windows.net
* [HybridConnectionName] - Name of a Hybrid Connection
* [ClientId] - Not applicable to System Assigned Identity
  - For User Assigned Identity, the ClientId associated with a User Assigned Identity resource.
  - For AAD, the Application (Client) ID associated with an AAD registered application
* [TenantId] - Directory (Tenant) ID associated with an AAD registered application. Only applicable to AAD Rbac.
* [ClientSecret] - Secret Value of a Client Secret associated with an AAD registered application. Only applicable to AAD Rbac.

Once started, a Relay Sender and a Relay listener would be created with the specified Rbac Authentication method

## System Assigned Identity

To run an application within an Azure resource with an enabled System Assigned Identity, please run the executable with the following arguments:

```RoleBasedAccessControl.exe [HostAddress] [HybridConnectionName]```

For example:

```RoleBasedAccessControl.exe sb://contoso.servicebus.windows.net myHc```

The below code creates and returns a TokenProvider object for a System Assigned Identity use case
```csharp
static TokenProvider GetManagedIdentityTokenProvider()
{
    return TokenProvider.CreateManagedIdentityTokenProvider();
}
```

## User Assigned Identity

To run an application with an Azure resource asociated with a User Assigned Identity, please run the executable with the following arguments: ClientId is associated with your User Assigned Identity resource

```RoleBasedAccessControl.exe [HostAddress] [HybridConnectionName] [ClientId]```

For example:

```RoleBasedAccessControl.exe sb://contoso.servicebus.windows.net myHc <YourClientId>```

The below code creates and returns a TokenProvider object for a User Assigned Identity use case

```csharp
static TokenProvider GetUserAssignedIdentityTokenProvider(string clientId)
{
    var managedCredential = new ManagedIdentityCredential(clientId);
    return TokenProvider.CreateManagedIdentityTokenProvider(managedCredential);
}
```

## AAD

To run an AAD registered application, please run the executable with the following arguments: Application (Client) ID , Directory (Tenant) ID is associated with your registered AAD application. ClientSecret is the Secret Value of a Client Secret associated with your AAD registered application 

```RoleBasedAccessControl.exe [HostAddress] [HybridConnectionName] [ClientId] [TenantId] [ClientSecret]```

For example:

```RoleBasedAccessControl.exe sb://contoso.servicebus.windows.net myHc <YourClientId> <YourTenantId> <YourClientSecret>```

The below code creates and returns a TokenProvider object for a User Assigned Identity use case

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
        $"https://login.microsoftonline.com/{tenantId}");
}
```

## Create a Relay Listener

The below code snippet creates a Relay Listener with the TokenProvider object returned by one of the above methods

```csharp
var listener = new HybridConnectionListener(new Uri($"{hostAddress}/{hybridConnectionName}"), tokenProvider);
```

## Create a Relay Sender

The below code snippet creates a Relay sender with the TokenProvider object returned by one of the above methods

```csharp
var sender = new HybridConnectionClient(new Uri($"{hostAddress}/{hybridConnectionName}"), tokenProvider);
```
