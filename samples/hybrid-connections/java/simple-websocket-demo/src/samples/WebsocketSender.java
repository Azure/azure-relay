package samples;

import java.net.URI;
import java.net.URISyntaxException;
import java.util.Map;
import java.util.Scanner;
import java.util.concurrent.ExecutionException;

import com.microsoft.azure.relay.ClientWebSocket;
import com.microsoft.azure.relay.HybridConnectionClient;
import com.microsoft.azure.relay.StringUtil;
import com.microsoft.azure.relay.TokenProvider;

public class WebsocketSender {
	static final String CONNECTION_STRING_ENV_VARIABLE_NAME = "RELAY_CONNECTION_STRING_ENVIRONMENT_VARIABLE";
	static final Map<String, String> connectionParams = StringUtil.parseConnectionString(System.getenv(CONNECTION_STRING_ENV_VARIABLE_NAME));
	static final String RELAY_NAME_SPACE = connectionParams.get("Endpoint");
	static final String CONNECTION_STRING = connectionParams.get("EntityPath");
	static final String KEY_NAME = connectionParams.get("SharedAccessKeyName");
	static final String KEY = connectionParams.get("SharedAccessKey");

	public static void main(String[] args) throws InterruptedException, ExecutionException, URISyntaxException {
		TokenProvider tokenProvider = TokenProvider.createSharedAccessSignatureTokenProvider(KEY_NAME, KEY);
		HybridConnectionClient client = new HybridConnectionClient(new URI(RELAY_NAME_SPACE + CONNECTION_STRING), tokenProvider);
		
		ClientWebSocket webSocket = new ClientWebSocket();
		Scanner in = new Scanner(System.in);
		
		webSocket.setOnMessage((msg) -> {
			System.out.println(msg);
		});
		
		client.createConnectionAsync(webSocket).thenAccept((socket) -> {
//			// Can also receive messages using the following way instead of calling setOnMessage() above:
//			socket.receiveMessageAsync().thenAccept((message) -> {
//				// Do stuff
//			});
			
			while (true) {
				System.out.println("Please enter the text you want to send.");
				String input = in.nextLine();
				webSocket.sendAsync(input).join();
				if (input.equals("quit")) break;
			}
			client.closeAsync();
			in.close();
		});	
	}

}
