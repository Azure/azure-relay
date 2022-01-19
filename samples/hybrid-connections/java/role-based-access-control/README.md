# Authenticating Hybrid Connection With RBAC (Role-Based Access Control) Sample 

Azure RBAC (Role-Based Access Control) is an authorization system built on Azure Resource Manager that provides fine-grained access management of Azure resources. To learn more, please check out the official introduction [Here](https://docs.microsoft.com/en-us/azure/role-based-access-control/overview).

In order to run this sample, first, you will need to gathered the connection parameters. Following the instructions [Here](https://github.com/azure-relay/tree/java-samples/samples/hybrid-connections/java).
`RELAY_CONNECTION_STRING` should be already set as an environment varaiable, which is used to identify the Hybrid Connection endpoint to connect to by the lines below:

```	java
static final String CONNECTION_STRING_ENV_VARIABLE_NAME = "RELAY_CONNECTION_STRING";
static final RelayConnectionStringBuilder CONNECTION_STRING_BUILDER = new RelayConnectionStringBuilder(
		System.getenv(CONNECTION_STRING_ENV_VARIABLE_NAME));
```

## Creating a TokenProvider with TokenCredential

A **TokenProvider** object is required to authenticate both the listener and sender clients, and it could be created with a **TokenCredential** object that's introduced by Azure Identity to support Azure RBAC.
It could be done with just one simple line below with the TokenCredential parameter provided by the user:

```java
TokenProvider tokenProvider = TokenProvider.createAzureIdentityTokenProvider(tokenCredential);
```

Below are some examples to demonstrate just some of the options that the user could use to create the **TokenCredential** object. For a more detailed and complete list, please see [Here](https://github.com/Azure/azure-sdk-for-java/wiki/Azure-Identity-Examples#authenticating-a-user-account-with-azure-cli).

### Authenticating with a ClientSecretTokenCredential

To create a ClientSecretTokenCredential, the user would need to create an app through Azure Active Directory , and obtain the following parameters from the app that's created: ClietId, ClientSecret, TenantId.
With these parameters, the ClientSecretTokenCredential object can be created as below:

```java
ClientSecretCredential clientSecretCredential = new ClientSecretCredentialBuilder()
		.clientId("<YOUR_CLIENT_ID>")
		.clientSecret("<YOUR_CLIENT_SECRET>")
		.tenantId("<YOUR_TENANT_ID>")
		.build();
```

### Authenticating with Azure Managed Identity

To create a TokenCredential with Azure Managed Identity, the user would need to first enable the system assigned identity on their Azure resource that's running the Hybrid Connection (a VM, for example).
After that, a ManagedIdentityCredential object can be simply created as below:

```java
ManagedIdentityCredential azureManagedIdentityCredential = new ManagedIdentityCredentialBuilder().build();
```

### Authenticating with User Assigned Managed Identity

To create a TokenCredential with User Assigned Managed Identity, the user would need to create and assign their user assigned managed identity to the Azure resource that's running the Hybrid Connection (a VM, for example).
After that, a ManagedIdentityCredential object can be simply created as below:

```java
ManagedIdentityCredential userAssignedIdentityCredential = new ManagedIdentityCredentialBuilder().clientId("<YOUR_USER_ASSIGNED_MANAGED_IDENTITY_CLIENT_ID>").build();
```