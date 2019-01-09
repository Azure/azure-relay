# 'Simple' Web Socket Hybrid Connection Sample 

First, you will need to gathered the connection parameters. Following the instructions [Here](https://github.com/azure-relay/tree/java-samples/samples/hybrid-connections/java), `RELAY_CONNECTION_STRING` should be already set as an environment varaiable for connection and authentication purposes, and the connection parameters can be retrieved as below:

```	java
	static final String CONNECTION_STRING_ENV_VARIABLE_NAME = "RELAY_CONNECTION_STRING";
	static final RelayConnectionStringBuilder connectionParams = new RelayConnectionStringBuilder(System.getenv(CONNECTION_STRING_ENV_VARIABLE_NAME));
```

## Listener 

This line below creates an instance of `HybridConnectionListener`. Using a `TokenProvider` instance is one of the ways to create a `HybridConnectionListener` instance with authentication required: 

```java
TokenProvider tokenProvider = TokenProvider.createSharedAccessSignatureTokenProvider(KEY_NAME, KEY);
HybridConnectionListener listener = new HybridConnectionListener(new URI(RELAY_NAMESPACE + ENTITY_PATH), tokenProvider);
```

With a listener instance created like above, we can now open the connection to Azure Relay and listen for any incoming connections:

```java
listener.openAsync();
```

This section below will wait for the user to press the ENTER key, then shuts the listener instance and closes any of its connections, also notifying all of its senders to close as well:

```java
CompletableFuture.runAsync(() -> {
	Scanner in = new Scanner(System.in);
	in.nextLine();
	
	listener.closeAsync().join();
	in.close();
});
```

Once the listener is connected to the Hybrid Connection, it can start accepting web socket connections asynchronously. This line below returns a CompletableFuture of a web socket instance once a connection is established, and this web socket instance will be able to both send and receive messages to and from the remote endpoint:

```java
listener.acceptConnectionAsync();
```

The code below will run on another thread after the web socket connection has been established. As long as the connection with the remote endpoint remains open, the web socket continuously listens for messages from the sender, then send back the message in prefix of "Echo: " in response.

```java
CompletableFuture.runAsync(() -> {
	System.out.println("New session connected.");
	
	while (websocket.isOpen()) {
		ByteBuffer bytesReceived = websocket.receiveMessageAsync().join();
		String msg = new String(bytesReceived.array());

		System.out.println("Received: " + msg);
		websocket.sendAsync("Echo: " + msg);
	}
	System.out.println("Session disconnected.");
});
```


## Sender

Similar to the Listener, this line below creates a `HybriConnectionClient` instance with authentication from `TokenProvider`:

```java
HybridConnectionClient client = new HybridConnectionClient(new URI(RELAY_NAMESPACE + ENTITY_PATH), tokenProvider);
```

Now we will connect the client instance to our Hybrid Connection. Once the connection is established, the CompletableFuture will complete with the corresponding web socket instance:

```java
client.createConnectionAsync()
```

With the established web socket connection, we are now continuously sending messages read from console input to the listener web socket and printing the echo messages to console:

```java
while (true) {
	System.out.println("Please enter the text you want to send, or enter \"quit\" or \"q\" to exit");
	String input = in.nextLine();
	if (input.equalsIgnoreCase("quit") || input.equalsIgnoreCase("q")) break;
	socket.sendAsync(input).join();
	
	socket.receiveMessageAsync().thenAccept((byteBuffer) -> {
		System.out.println("Received: " + new String(byteBuffer.array()));
	});
}
```

In the end, we will terminate the web socket instance to terminate the application. This will also terminate its connection with the remote endpoint:

```java
client.closeAsync();
```
