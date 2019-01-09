# 'Simple' HTTP Hybrid Connection Sample 

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

This handler function will trigger once the http request is received. The context object is a combination of the incoming request and outgoing response objects. The handler method should be attached to the listener before the listener is opened:

```java
listener.setRequestHandler((context) -> {
	
	// Handles the incoming request here
});
```

Inside the request handler, we will read and print the incoming bytes from the request object from `context`, then set the status code ad status description to the response object from `context` as well. With the received message, we will then write to the output stream of the response as an echo in the form of bytes. After finishing writing to the output stream within the response object, we must close the response object in order for the written messages to be flushed:

```java
    ByteBuffer inputStream = context.getRequest().getInputStream();
    String receivedText = (inputStream != null) ? new String(inputStream.array()) : "";
    System.out.println("requestHandler received " + receivedText);
    
    RelayedHttpListenerResponse response = context.getResponse();
    response.setStatusCode(202);
    response.setStatusDescription("OK");
    
    try {
		response.getOutputStream().write(("Echo: " + receivedText).getBytes());
	 } catch (IOException e) {
		e.printStackTrace();
	 }
	 
	 response.close();
```

With the request handler ready, the listener can be opened now to connect to Azure Relay and listen for any incoming connections:

```java
listener.openAsync();
```

Shut down the listener instance in the end to stop accepting any more requests or connections:

```java
listener.closeAsync();
```


## Sender

This line below will create the url for reaching the Hybrid Connection HTTP endpoint:

```java
String urlString = connectionParams.getHttpUrlString();
```

The lines below are used to create a HTTP connection instance that requires authentication. Note that the authenticating token need to be set as the request header before the request is sent, and the `conn.setDoOutput(true)` line is needed to send any output successfully:

```java
String tokenString = tokenProvider.getTokenAsync(urlString, Duration.ofHours(1)).join().getToken();

HttpURLConnection conn = (HttpURLConnection)new URL(urlString).openConnection();
conn.setRequestMethod(StringUtil.isNullOrEmpty(message) ? "GET" : "POST");
conn.setRequestProperty("ServiceBusAuthorization", tokenString);
conn.setDoOutput(true);
```

We can use a regular `OutputStreamWriter` object to write the message for our request as shown below:

```java
OutputStreamWriter out = new OutputStreamWriter(conn.getOutputStream());
out.write(message, 0, message.length());
out.flush();
out.close();
```

A normal `InputStreamReader` can be used to read the inputs that are coming from our connection instance:

```java
	String inputLine;
	StringBuilder responseBuilder = new StringBuilder();
	BufferedReader inStream = new BufferedReader(new InputStreamReader(conn.getInputStream()));
	
	System.out.println("status code: " + conn.getResponseCode());
	while ((inputLine = inStream.readLine()) != null) {
		responseBuilder.append(inputLine);
	}
```
