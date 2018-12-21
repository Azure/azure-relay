# 'Simple' Web Socket Hybrid Connection Sample 

First, as described in the description [Here](https://github.com/bainian12345/azure-relay/tree/java-samples/samples/hybrid-connections/java), you will need to retrieve different parameters from the enviroment variable `RELAY_CONNECTION_STRING` for connection and authentication purposes:

```	java
	static final String CONNECTION_STRING_ENV_VARIABLE_NAME = "RELAY_CONNECTION_STRING";
	static final Map<String, String> connectionParams = HybridConnectionUtil.parseConnectionString(System.getenv(CONNECTION_STRING_ENV_VARIABLE_NAME));
	static final String RELAY_NAMESPACE = connectionParams.get("Endpoint");
	static final String ENTITY_PATH = connectionParams.get("EntityPath");
	static final String KEY_NAME = connectionParams.get("SharedAccessKeyName");
	static final String KEY = connectionParams.get("SharedAccessKey");
```

For Hybrid Connection with authorization required, `TokenProvider` will be required to authenticate connections made to Azure Relay:

```
TokenProvider tokenProvider = TokenProvider.createSharedAccessSignatureTokenProvider(KEY_NAME, KEY);
```

## Listener 

Creates an instance of `HybridConnectionListener` which requires authentication from a `TokenProvider`: 

```
HybridConnectionListener listener = new HybridConnectionListener(new URI(RELAY_NAMESPACE + ENTITY_PATH), tokenProvider);
```

Opens the connection to Azure Relay and listens for any incoming connections:

```
listener.openAsync();
```

Once the listner is connected to the Hybrid Connection, it can start accepting web socket connections asynchronously, then returns the web socket instance once a connection is established:

```
listener.acceptConnectionAsync();
```


This handler function will trigger once the http request is received. The context object is a combination of the incoming request and outgoing response:

```
listener.setRequestHandler((context) -> {});
```

Blocks until a message is received from the web socket, and returns the message as a `ByteBuffer`:

```
ByteBuffer bytesReceived = websocket.receiveMessageAsync().join();
```

Sends the message asynchronously to the remote web socket:

```
websocket.sendAsync("Echo: " + msg);
```

Shuts the listener instance and closes any of its connections, also notifies all of its senders to close as well:

```
listener.closeAsync().join();
```

## Sender

Establishes a `HybriConnectionClient` instance with authentication from `TokenProvider`:

```
TokenProvider tokenProvider = TokenProvider.createSharedAccessSignatureTokenProvider(KEY_NAME, KEY);
HybridConnectionClient client = new HybridConnectionClient(new URI(RELAY_NAMESPACE + ENTITY_PATH), tokenProvider);
```

Creates a connection to the Hybrid Connection instance and returns the corresponding web socket instance once the connection is established:

```
client.createConnectionAsync()
```

Receives the next complete message sent from the remote web socket. Should be put in a loop to continuously receive messages:

```
socket.receiveMessageAsync()
```

Closes the `HybridConnectionClient` instance and its connections:

```
client.closeAsync();
```
