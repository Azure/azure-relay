# Role Based Access Control Sample with HYCO-HTTPS

This sample illustrates how to authenticate with the 'hyco-https' package using RBAC (role-based access control) mechanisms.
For this sample, we will show how to authenticate with ClientSecretCredential, user-assigned ManagedIsdentityCredential, and Azure-assigned ManagedIdentityCredential.
More options to authenticate with RBAC token credentials can be found [here](https://docs.microsoft.com/en-us/javascript/api/overview/azure/identity-readme?view=azure-node-latest).

## ClientSecretCredential

To authenticate with ClientSecretCredential, you must first create an Azure Active Directory application, then assign the appropriate role to your Relay namespace or HybridConnection as described [here](https://docs.microsoft.com/en-us/azure/azure-relay/authenticate-application). You can then run the listener and sender samples through command line by supplying the necessary arguments. Below are examples of the command line commands used to run the listener and sender samples (replace the value in <> braces with your values).

```
node listener.js --ns=<RELAY_NAMESPACE_NAME>.servicebus.windows.net --path=<HYBRID_CONNECTION_NAME> --tenantid=<AAD_APP_TENANT_ID> --clientid=<AAD_APP_CLIENT_ID> --clientsecret=<AAD_APP_CLIENT_SECRET>
```

```
node https-sender.js --ns=<RELAY_NAMESPACE_NAME>.servicebus.windows.net --path=<HYBRID_CONNECTION_NAME> --tenantid=<AAD_APP_TENANT_ID> --clientid=<AAD_APP_CLIENT_ID> --clientsecret=<AAD_APP_CLIENT_SECRET>
```

## User-Assigned ManagedIdentityCredential

To authenticate with user-assigned ManagedIdentityCredential, you must first create a ManagedIdentity through Azure, then enable user-assigned ManagedIdentity on your Azure resource and assign the appropriate user-assigned ManagedIdentity to your Relay namespace or HybridConnection as described [here](https://docs.microsoft.com/en-us/azure/azure-relay/authenticate-managed-identity). You can then run the listener and sender samples **from your Azure resource** through command line by supplying the necessary arguments. Below are examples of the command line commands used to run the listener and sender samples (replace the value in <> braces with your values).

```
node listener.js --ns=<RELAY_NAMESPACE_NAME>.servicebus.windows.net --path=<HYBRID_CONNECTION_NAME> --clientid=<MANAGED_IDENTITY_CLIENT_ID>
```

```
node https-sender.js --ns=<RELAY_NAMESPACE_NAME>.servicebus.windows.net --path=<HYBRID_CONNECTION_NAME> --clientid=<MANAGED_IDENTITY_CLIENT_ID>
```

## Azure-Assigned ManagedIdentityCredential

To authenticate with Azure-assigned (system-assigned) ManagedIdentityCredential, you must enable Azure-assigned (system-assigned) ManagedIdentity on your Azure resource as described [here](https://docs.microsoft.com/en-us/azure/azure-relay/authenticate-managed-identity). You can then run the listener and sender samples **from your Azure resource** through command line by supplying the necessary arguments. Below are examples of the command line commands used to run the listener and sender samples (replace the value in <> braces with your values).

```
node listener.js --ns=<RELAY_NAMESPACE_NAME>.servicebus.windows.net --path=<HYBRID_CONNECTION_NAME>
```

```
node https-sender.js --ns=<RELAY_NAMESPACE_NAME>.servicebus.windows.net --path=<HYBRID_CONNECTION_NAME>
```
