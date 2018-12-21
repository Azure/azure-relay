# 'Simple' HTTP Hybrid Connection Sample 

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

This handler function will trigger once the http request is received. The context object is a combination of the incoming request and outgoing response:

```
listener.setRequestHandler((context) -> {});
```

Gets the incoming bytes from the request:

```
ByteBuffer inputStream = context.getRequest().getInputStream();
```

Gets the output stream and writes to the response:

```
response.getOutputStream().write(("Echo: " + receivedText).getBytes());
```

The response must be closed, and before it closes the message written will be flushed and sent:

```
response.close();
```

Opens the connection to Azure Relay and listens for any incoming connections:

```
listener.openAsync();
```

Shuts down the listener instance and stops accepting any more requests or connections:

```
listener.closeAsync();
```


## Sender

Establishes a HTTP connection to the server:

```
String urlString = HybridConnectionUtil.getURLString(RELAY_NAMESPACE, ENTITY_PATH);
HttpURLConnection conn = (HttpURLConnection)new URL(urlString).openConnection();
```

Must set token string obtained from token provider to request header as part of the required authorization:

```
String tokenString = tokenProvider.getTokenAsync(urlString, Duration.ofHours(1)).join().getToken();
conn.setRequestProperty("ServiceBusAuthorization", tokenString);
```

Need to set this to successfully send any output:

```
conn.setDoOutput(true);
```

Write to the request stream using a regular `OutputStreamWriter`:

```
OutputStreamWriter out = new OutputStreamWriter(conn.getOutputStream());
out.write(message, 0, message.length());
out.flush();
out.close();
```

Read the response as any other HTTP handler would from an input stream:

```
BufferedReader inStream = new BufferedReader(new InputStreamReader(conn.getInputStream()));
```
