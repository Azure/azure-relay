# Relay Authorization Sample - Connection Strings

This is a variation of the [RelayTcp](../RelayTcp/README.md) sample that is generally identical in structure
to that baseline.

This sample differs in how the client and the server programs are set up. The Service Bus portal and some 
of the other tools hand out connection information for Service Bus in a single, handy string 
format: "connection strings".

A connection string contains the fully qualified base URI for the Service Bus namespace, an optional entity path,
and credentials (or outright tokens) for either the Shared Access or Shared Secret authorization model. The 
connection string format is a simple {property}={value} notation, separated by semicolons. The connection string 
with the "master" shared access signature key for an Azure Service Bus namespace looks similar to this

```
Endpoint=sb://namespacename.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=/383tdKi9e9nXkvB0bhRQu3exkfckBmIjuEGJY2aQnI=
```

The relevant properties for the Service Bus Relay are:

| Property name              | Description                                             |   
|----------------------------|---------------------------------------------------------|
| Endpoint                   | Fully qualified base URI for the Service Bus namespace  |
| EntityPath                 | Path to a particular entity in the namespace (optional) |
| SharedAccessKey            | Shared access signature rule key                        |
| SharedAccessKeyName        | Shared access signature rule name                       |
| SharedAccessSignature      | Pre-issued shared access signature token                |
| SharedSecretIssuerName     | Issuer name (account name) for an [AAD ACS](https://azure.microsoft.com/en-us/documentation/articles/active-directory-dotnet-how-to-use-access-control/) service identity |
| SharedSecretIssuerSecret   | Passphrase for AAD ACS service identity                 |
 
If you are using the [ServiceBusConnectionStringBuilder](https://msdn.microsoft.com/library/microsoft.servicebus.servicebusconnectionstringbuilder.aspx) class 
you will notice several extra properties. All of those are only applicable to Service Bus for Windows Server, where the Relay is not available.

> **Important Note**
> Service Bus has historically integrated quite tightly with the Azure Active Directory (AAD) Access Control Service (ACS). Many older samples you may find across the web use this integration with "issuer key" and "issuer secret", referring to ACS service identities. 
> As the Active Directory Team explains [in a blog post](http://blogs.technet.com/b/ad/archive/2015/02/12/the-future-of-azure-acs-is-azure-active-directory.aspx), the capabilities of ACS will transition into AAD, after which ACS will be phased out. 
> Therefore, the Service Bus tooling will generally no longer create ACS-federated namespaces, but only support Shared Access Signature by default. To support existing applications, ACS-federated namespaces can be created using the [new-AzureSBNamespace](https://msdn.microsoft.com/library/azure/dn495165.aspx) Powershell cmdlet.

## Handling Connection Strings

When you add the [Azure Service Bus NuGet package](https://www.nuget.org/packages/WindowsAzure.ServiceBus/) to a .NET project with an existing *app.config* or *web.config* file, 
you will find the "Microsoft.ServiceBus.ConnectionString" key added or merged into the appSettings element:

```XML    
    <appSettings>
        <!-- Service Bus specific app setings for messaging connections -->
        <add key="Microsoft.ServiceBus.ConnectionString"
            value="Endpoint=sb://[your namespace].servicebus.windows.net;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=[your secret]"/>
    </appSettings>
```    

For Azure cloud projects, the preferred location for this setting is in the *ServiceConfiguration.cscfg* file, within a Role's settings:
```XML    
   <Role name="example">
     <Instances count="1" />
     <ConfigurationSettings>
       <Setting name="Microsoft.ServiceBus.ConnectionString" 
                value="Endpoint=sb://[your namespace].servicebus.windows.net;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=[your key]" />
     </ConfigurationSettings>
   </Role>
```
To obtain the connection string string value use <code>System.Configuration.ConfigurationManager</code> 
```csharp
    connectionString = ConfigurationManager.AppSettings["Microsoft.ServiceBus.ConnectionString"];          
```

in an Azure project, use the <code>Microsoft.WindowsAzure.CloudConfigurationManager</code> API

```csharp
    connectionString = CloudConfigurationManager.GetSetting("Microsoft.ServiceBus.ConnectionString");
```

The Azure Service Bus Relay API, does not have a *FromConnectionString* helper method to crack the 
connection string, so this sample shows you how to handle those easily.

> <span style="color:red">**Security Note**</span>
> Storing connection strings in configuration files or other files that are part of the project can be risky and often lead to inadvertant disclosure of secrets into source code repositories. 
> For **server- and cloud-hosted software**, it is **strongly recommended** to only merge connection strings and other credentials into the configuration files at deployment time and to never submit the resultant files into source control.
> For **client applications**, credentials **should never reside in configuration files the application directory**, but should be stored securely in the user profile.
> Generally, the Service Bus ***RootManageSharedAccessKey*** should <span style="color:red">**NEVER**</span> be used directly in any deployed applications. Instead, create appropriate dedicated rules for the application, and preferably use pre-issued SAS signatures rather than raw keys; [review the SAS example for more information](../RelayAuthorizationSAS/README.md)

## Client and Server

The sample's main entry point is the shared [Main.cs](../common/Main.md) code that is also used for 
all other Relay samples. It generates a connection string from the available configuration and then 
passes it to the respective <code>Run()</code> entry points of the [client](./client/Program.cs) and [server](./client/Program.cs) Program.cs files.

The input connection string is first parsed using the connection string builder:
```csharp
        var connectionStringBuilder = new ServiceBusConnectionStringBuilder(connectionString);
```

We then construct the address from the first available runtime endpoint (the API uses a collection since Service 
Bus for Windows Server supports multiple endpoints here) and optionally add the supplied entity path. If there's no
entity path, we'll use "relay".

```csharp
        var address = new UriBuilder(connectionStringBuilder.GetAbsoluteRuntimeEndpoints()[0])
        {
            Path = connectionStringBuilder.EntityPath ?? "relay"
        }.ToString();
```

The remaining special logic is about creating a token provider instance from the credentials information. If
a SharedAccessKeyName and SharedAccessKey value is present, or if we find a pre-issued SharedAccessSignature,
we'll create a SAS token provider. For ACS-federated namespaces with SharedSecretIssuerName and SharedIssuerSecret
we create a sharded-secret token provider.

```csharp
        TokenProvider tokenProvider = null;
        if (connectionStringBuilder.SharedAccessKeyName != null &&
            connectionStringBuilder.SharedAccessKey != null)
        {
            tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(connectionStringBuilder.SharedAccessKeyName, connectionStringBuilder.SharedAccessKey);
        }
        else if (connectionStringBuilder.SharedAccessSignature != null)
        {
            tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(connectionStringBuilder.SharedAccessSignature);
        }
        else if (connectionStringBuilder.SharedSecretIssuerName != null && 
                    connectionStringBuilder.SharedSecretIssuerSecret != null)
        {
            tokenProvider = TokenProvider.CreateSharedSecretTokenProvider(connectionStringBuilder.SharedSecretIssuerName, connectionStringBuilder.SharedSecretIssuerSecret);
        }
```

In the client application, we're adding the token provider to the channel factory:

```csharp
    var cf = new ChannelFactory<IClient>(
                    new NetTcpRelayBinding {IsDynamic = true},
                    sendAddress);

    cf.Endpoint.EndpointBehaviors.Add(new TransportClientEndpointBehavior(tokenProvider)); 
```

In the server application, we add it to the endpoint:

```csharp
    host.AddServiceEndpoint(
        GetType(),
        new NetTcpRelayBinding {IsDynamic = true},
        listenAddress)
        .EndpointBehaviors.Add(
            new TransportClientEndpointBehavior(tokenProvider));
```

## Running the sample

You can run the client from Visual Studio or on the command line from the sample's root directory by starting <code>client/bin/debug/client.exe</code>. You
must run the service from Visual Studio or the command line, preferably with <code>start service/bin/debug/service.exe</code> into a separate, parallel command window, and the service must report itself as listening
before you can start the client.
