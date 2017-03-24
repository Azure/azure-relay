# Relay Authorization Sample - Shared Access Signature (SAS)

This sample shows how to explicitly use the Shared Access Signature token provider
in your applications. It is a variation of the [RelayTcp](../RelayTcp/README.md) sample, and structually identical
to that baseline.

The Shared Access Signature (SAS) authorization method for Service Bus provides a way to tie named rules to
specific access rights, and associate a primary and a secondary signing key with that rule.

While SAS rules look similar to accounts with passphrases, they work differently. Passphrases are passed to
the server. SAS rule keys are used to sign a token that is passed to the server along with the rule name, so
that the secret (the key) never gets transferred over the wire.

A Service Bus namespace can hold at most 12 rules at the root, which are scoped to the whole namespace and
can therefore confer access rights to all entities within that namespace.

The [CreateServiceBusRelay.ps1](../scripts/azure/servicebus/CreateServiceBusRelay.ps1) script that is used in
the sample setup procedure, shows how to create such rules using Powershell:

```Powershell
New-AzureSBAuthorizationRule -Name "$SendRuleName" -Namespace $Namespace -Permission $("Send") -PrimaryKey $SendKey
New-AzureSBAuthorizationRule -Name "$ListenRuleName" -Namespace $Namespace -Permission $("Listen") -PrimaryKey $ListenKey
New-AzureSBAuthorizationRule -Name "$ManageRuleName" -Namespace $Namespace -Permission $("Manage", "Listen","Send") -PrimaryKey $ManageKey
```

Each entity in Service Bus can be configured with a further set of up to 12 local SAS rules. All entity description classes, including
[RelayDescription](https://msdn.microsoft.com/library/microsoft.servicebus.messaging.relaydescription.aspx) have an [Authorization](https://msdn.microsoft.com/en-us/library/microsoft.servicebus.messaging.relaydescription.authorization.aspx)
property that holds a collection of [AuthorizationRule](https://msdn.microsoft.com/en-us/library/microsoft.servicebus.messaging.authorizationrule.aspx) objects. When you preconfigure [a Relay entity
in Service Bus with the Namespace Manager](https://msdn.microsoft.com/library/azure/dn339410.aspx), you can add one or more [SharedAccessAuthorizationRule](https://msdn.microsoft.com/en-us/library/microsoft.servicebus.messaging.sharedaccessauthorizationrule.aspx) instances
on this property that will then be scoped only to this Relay. You can always also use the existing rules at the root of the namespace to create valid tokens for
the newly created entity.

The Relay is the only capability in Service Bus that allows for dynamic entities that do not require preconfiguration. Dynamic entities cannot have
local SAS rules, so you must rely on the rules applied to the namespace root.

It's important to note that the signing key is not required to
reside on the client, at all. The client only needs to be in possession of a valid token that can be issued
by a token service holding on to the keys. That model is the default for most other Relay samples, in this
sample you see how to use the Service Bus <code>TokenProvider</code> factory class with a SAS rule name and a rule key value.

## Client

On the [client-side of the sample](./client/Program.cs), the <code>Run()</code> method is invoked with the name of a SAS rule and the associated
SAS rule key by the [shared entry point](../common/Main.md).

The client creates a new channel factory using a non-dynamic (preconfigured) TCP relay binding, and then adds a
token provider created from the SAS rule name and SAS rule key.

```csharp
var cf = new ChannelFactory<IClient>(
    new NetTcpRelayBinding {IsDynamic = false},
    sendAddress);

    cf.Endpoint.EndpointBehaviors.Add(
    new TransportClientEndpointBehavior(
        TokenProvider.CreateSharedAccessSignatureTokenProvider(sendKeyName, sendKeyValue)));
```

## Service

On the [service-side of the sample](./client/Program.cs), the <code>Run()</code> method is likewise invoked with the name of a SAS rule and the associated
SAS rule key by the [shared entry point](../common/Main.md).

The sample adds the service endpoint to the host and adds the SAS token provider with SAS rule name and key all in one go:

```csharp
host.AddServiceEndpoint(
    GetType(),
    new NetTcpRelayBinding {IsDynamic = false},
        listenAddress)
        .EndpointBehaviors.Add(
            new TransportClientEndpointBehavior(
            TokenProvider.CreateSharedAccessSignatureTokenProvider(listenKeyName, listenKeyValue)));
```

## Running the sample

You can run the client from Visual Studio or on the command line from the sample's root directory by starting <code>client/bin/debug/client.exe</code>. You
must run the service from Visual Studio or the command line, preferably with <code>start service/bin/debug/service.exe</code> into a separate, parallel command window, and the service must report itself as listening
before you can start the client.
