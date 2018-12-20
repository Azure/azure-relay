package samples;

import java.net.URI;
import java.net.URISyntaxException;
import java.util.Map;
import java.util.Scanner;
import java.util.concurrent.ExecutionException;

import com.microsoft.azure.relay.HybridConnectionClient;
import com.microsoft.azure.relay.HybridConnectionUtil;
import com.microsoft.azure.relay.TokenProvider;

public class WebsocketSender {
	static final String CONNECTION_STRING_ENV_VARIABLE_NAME = "RELAY_CONNECTION_STRING";
	static final Map<String, String> connectionParams = HybridConnectionUtil.parseConnectionString(System.getenv(CONNECTION_STRING_ENV_VARIABLE_NAME));
	static final String RELAY_NAMESPACE = connectionParams.get("Endpoint");
	static final String ENTITY_PATH = connectionParams.get("EntityPath");
	static final String KEY_NAME = connectionParams.get("SharedAccessKeyName");
	static final String KEY = connectionParams.get("SharedAccessKey");

	public static void main(String[] args) throws InterruptedException, ExecutionException, URISyntaxException {
		TokenProvider tokenProvider = TokenProvider.createSharedAccessSignatureTokenProvider(KEY_NAME, KEY);
		HybridConnectionClient client = new HybridConnectionClient(new URI(RELAY_NAMESPACE + ENTITY_PATH), tokenProvider);
		
		Scanner in = new Scanner(System.in);
		
		client.createConnectionAsync().thenAccept((socket) -> {
			while (true) {
				System.out.println("Please enter the text you want to send, or enter \"quit\" or \"q\" to exit");
				String input = in.nextLine();
				if (input.equalsIgnoreCase("quit") || input.equalsIgnoreCase("q")) break;
				socket.sendAsync(input).join();
				
				socket.receiveMessageAsync().thenAccept((byteBuffer) -> {
					System.out.println("Received: " + new String(byteBuffer.array()));
				});
			}
			
			client.closeAsync().join();
			in.close();
		});	
	}
}
