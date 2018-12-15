package samples;

import java.net.URI;
import java.net.URISyntaxException;
import java.nio.ByteBuffer;
import java.util.Map;
import java.util.concurrent.CompletableFuture;

import com.microsoft.azure.relay.ClientWebSocket;
import com.microsoft.azure.relay.HybridConnectionListener;
import com.microsoft.azure.relay.StringUtil;
import com.microsoft.azure.relay.TokenProvider;

public class WebsocketListener {
	static boolean quit = false;
	
	static final String CONNECTION_STRING_ENV_VARIABLE_NAME = "RELAY_CONNECTION_STRING_ENVIRONMENT_VARIABLE";
	static final Map<String, String> connectionParams = StringUtil.parseConnectionString(System.getenv(CONNECTION_STRING_ENV_VARIABLE_NAME));
	static final String RELAY_NAME_SPACE = connectionParams.get("Endpoint");
	static final String CONNECTION_STRING = connectionParams.get("EntityPath");
	static final String KEY_NAME = connectionParams.get("SharedAccessKeyName");
	static final String KEY = connectionParams.get("SharedAccessKey");
	
	public static void main(String[] args) throws URISyntaxException {
		TokenProvider tokenProvider = TokenProvider.createSharedAccessSignatureTokenProvider(KEY_NAME, KEY);
		HybridConnectionListener listener = new HybridConnectionListener(new URI(RELAY_NAME_SPACE + CONNECTION_STRING), tokenProvider);
        
        listener.openAsync().join();
        while (!quit) {
        	ClientWebSocket websocket = listener.acceptConnectionAsync().join();
			System.out.println("New session connected.");
			while (websocket.isOpen()) {
				System.out.println("socket is open");
				ByteBuffer bytesReceived = websocket.receiveMessageAsync().join();
				System.out.println("msg received");
				String msg = new String(bytesReceived.array());
				if (msg.equals("quit")) {
					System.out.println("quit!");
					quit = true;
					break;
				}
				System.out.println("Received: " + msg);
				websocket.sendAsync("Echo: " + msg);
			}
        }
        listener.closeAsync();
	}
}
