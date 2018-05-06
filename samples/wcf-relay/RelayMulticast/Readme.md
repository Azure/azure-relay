# Azure Service Bus Multicast Sample

This sample demonstrates using the Relay's **NetEventRelayBinding** for the 
Azure Service Bus Relay. This binding allows multiple applications to listen to 
one-way events sent to an endpoint; events sent to that endpoint are received by all 
applications. it implements a minimal but functional chat room.

> **Note**
> This capability predates the existence of Service Bus' Messaging Topics. It is 
> generally recommended to prefer Topics and Subscriptions for multicast message 
> distribution scenarios in new applications. The NetEventRelayBinding is meant 
> to serve as a plug-in replacement for datagram communication, especially for 
> applications that use the WCF UdpBinding in Multicast mode, which is unavailable 
> in Azure. The NetEventRelayBinding is TCP based and has slightly lower latency 
> than Topics, but is constrained to at most 25 concurrent listeners on a name.  

##Implementation
This sample's Program class implements a chatroom with the <code>IChat</code> contract. 
<code>Hello</code> and <code>Bye</code> are used within the chatroom application to 
notify participants when a user joins and leaves the chat. <code>Chat</code> is 
called by the application when a user provides a string to contribute to the 
conversation.

Note that the methods are and must be marked as <code>IsOneWay=True</code> for use with
the *NetEventRelayBinding*
```csharp
[ServiceContract(Name = "IChat", Namespace = "")]
public interface IChat
{
     [OperationContract(IsOneWay=true)]
     void Hello(string nickName);
 
     [OperationContract(IsOneWay = true)]
     void Chat(string nickName, string text);
 
     [OperationContract(IsOneWay = true)]
     void Bye(string nickName);
}
```

The service implementation of the chat room is trivial and prints the received 
messages on the console. The "driver" is a simple loop that terminates on a blank line

```csharp
void RunChat(IChat channel, string chatNickname)
{
    Console.WriteLine("\nPress [Enter] to exit\n");
    channel.Hello(chatNickname);

    string input = Console.ReadLine();
    while (input != string.Empty)
    {
        channel.Chat(chatNickname, input);
        input = Console.ReadLine();
    }
    channel.Bye(chatNickname);
}
```

The service and client implementation is all in one place, since the chat room client
is obviously both multicast sender and multicast receiver on the same name.

We make a dynamic service address for each chat room. All clients that use the same address
will join the same chat room. The SAS rule fed into the token provider must at least grant
"Send, Listen" permissions on the namespace.

```csharp
var serviceAddress = new UriBuilder("sb", serviceBusHostName, -1, "/chat/" + session).ToString();
var netEventRelayBinding = new NetEventRelayBinding();
var tokenBehavior = new TransportClientEndpointBehavior(TokenProvider.CreateSharedAccessSignatureTokenProvider(token));

using (var host = new ServiceHost(this))
{
    host.AddServiceEndpoint(typeof(IChat), netEventRelayBinding, serviceAddress)
        .EndpointBehaviors.Add(tokenBehavior);
    host.Open();

    using (var channelFactory = new ChannelFactory<IChat>(netEventRelayBinding, serviceAddress))
    {
        channelFactory.Endpoint.Behaviors.Add(tokenBehavior);
        var channel = channelFactory.CreateChannel();

        this.RunChat(channel, chatNickname);

        channelFactory.Close();
    }
    host.Close();
}
return Task.FromResult(true);
}
```

## Running the sample

Run the samples multiple times to impersonate multiple users chatting. You can obviously also 
break the sample out of the shared entry point harness and run it on multiple machines for
an actual chat session.