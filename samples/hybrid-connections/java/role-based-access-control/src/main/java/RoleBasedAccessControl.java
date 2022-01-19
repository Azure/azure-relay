// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import java.io.IOException;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URI;
import java.net.URISyntaxException;
import java.net.URL;
import java.nio.ByteBuffer;
import java.time.Duration;
import com.azure.core.credential.TokenCredential;
import com.azure.identity.ClientSecretCredentialBuilder;
import com.azure.identity.ManagedIdentityCredentialBuilder;
import com.microsoft.azure.relay.HybridConnectionClient;
import com.microsoft.azure.relay.HybridConnectionListener;
import com.microsoft.azure.relay.RelayConnectionStringBuilder;
import com.microsoft.azure.relay.TokenProvider;

public class RoleBasedAccessControl {
	static final String CONNECTION_STRING_ENV_VARIABLE_NAME = "RELAY_CONNECTION_STRING";
	static final RelayConnectionStringBuilder CONNECTION_STRING_BUILDER = new RelayConnectionStringBuilder(
			System.getenv(CONNECTION_STRING_ENV_VARIABLE_NAME));
	
	// These are just the potential TokenCredential options that you can use for authentication, any one of them could be used.
	// For more TokenCredential options, please see https://github.com/Azure/azure-sdk-for-java/wiki/Azure-Identity-Examples#authenticating-a-user-account-with-azure-cli.
	public static void main(String[] args) {
		String clientId = System.getenv("RELAY_AAD_CLIENTID");
		String clientSecret = System.getenv("RELAY_AAD_CLIENT_SECRET");
		String tenantId = System.getenv("RELAY_AAD_TENANTID");
		
		TokenCredential tokenCredential = null;
		if (clientId != null && clientSecret != null && tenantId != null) {
			System.out.println("Building a TokenCredential using ClientSecret");
			tokenCredential = new ClientSecretCredentialBuilder()
			        .clientId(clientId)
			        .clientSecret(clientSecret)
			        .tenantId(tenantId)
			        .build();
		} else if (clientId != null) {
			System.out.println("Building a TokenCredential using User-Assigned ManagedIdentity");
			tokenCredential = new ManagedIdentityCredentialBuilder().clientId(clientId).build();
		} else {
			System.out.println("Building a TokenCredential using System-Assigned ManagedIdentity");
			tokenCredential = new ManagedIdentityCredentialBuilder().build();
		}
		
		HybridConnectionListener listener = null;
		try {
			listener = createAndOpenListenerWithTokenCrendential(tokenCredential);
			openAndSendWithWebsocketClient(tokenCredential);
			sendHttpRequestWithTokenCredential(tokenCredential);
		} catch (Exception e) {
			e.printStackTrace();
		} finally {
			if (listener != null) listener.close();
		}
	}
	
	public static HybridConnectionListener createAndOpenListenerWithTokenCrendential(TokenCredential tokenCredential) throws URISyntaxException {
		System.out.println("Test opening the listener with a TokenProvider created using the TokenCredential...");
		TokenProvider tokenProvider = TokenProvider.createAzureIdentityTokenProvider(tokenCredential);
		HybridConnectionListener listener = new HybridConnectionListener(new URI(CONNECTION_STRING_BUILDER.getEndpoint() + CONNECTION_STRING_BUILDER.getEntityPath()), tokenProvider);
		listener.openAsync(Duration.ofSeconds(15)).join();
		System.out.println("Listener opened.");
		return listener;
	}

	public static void openAndSendWithWebsocketClient(TokenCredential tokenCredential) throws URISyntaxException {
		System.out.println("Test opening the websocket sender with the new TokenProvider...");
		TokenProvider tokenProvider = TokenProvider.createAzureIdentityTokenProvider(tokenCredential);
		HybridConnectionClient client = new HybridConnectionClient(new URI(CONNECTION_STRING_BUILDER.getEndpoint() + CONNECTION_STRING_BUILDER.getEntityPath()), tokenProvider);
		client.createConnectionAsync().thenCompose(channel -> {
			return channel.writeAsync(ByteBuffer.wrap("Hello".getBytes())).thenCompose($null -> channel.closeAsync());
		}).join();
		System.out.println("Opened and sent with websocket client.");
	}
	
	public static void sendHttpRequestWithTokenCredential(TokenCredential tokenCredential) throws IOException {
		System.out.println("Test sending HTTP message with the new TokenProvider...");
		TokenProvider tokenProvider = TokenProvider.createAzureIdentityTokenProvider(tokenCredential);

		StringBuilder urlBuilder = new StringBuilder(CONNECTION_STRING_BUILDER.getEndpoint() + CONNECTION_STRING_BUILDER.getEntityPath());
		urlBuilder.replace(0, 5, "https://");
		URL url = new URL(urlBuilder.toString());
		
		HttpURLConnection connection = (HttpURLConnection)url.openConnection();
		String tokenString = tokenProvider.getTokenAsync(url.toString(), Duration.ofHours(1)).join().getToken();
		connection.setRequestProperty("ServiceBusAuthorization", tokenString);
		connection.setRequestMethod("POST");
		connection.setDoOutput(true);
		OutputStream out = connection.getOutputStream();
		byte[] bytes = "Hello HTTP request".getBytes();
		out.write(bytes, 0, bytes.length);
		out.flush();
		out.close();
		System.out.println("Sent the HTTP request.");
	}
}
