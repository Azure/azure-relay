package samples;

import java.net.URI;
import java.net.URISyntaxException;
import java.nio.ByteBuffer;
import java.util.Scanner;
import java.util.concurrent.CompletableFuture;

import com.microsoft.azure.relay.ClientWebSocket;
import com.microsoft.azure.relay.HybridConnectionListener;
import com.microsoft.azure.relay.RelayConnectionStringBuilder;
import com.microsoft.azure.relay.TokenProvider;

public class WebsocketListener {
	static boolean quit = false;
	
	static final String CONNECTION_STRING_ENV_VARIABLE_NAME = "RELAY_CONNECTION_STRING";
	static final RelayConnectionStringBuilder connectionParams = new RelayConnectionStringBuilder(System.getenv(CONNECTION_STRING_ENV_VARIABLE_NAME));
	
	public static void main(String[] args) throws URISyntaxException {
		TokenProvider tokenProvider = TokenProvider.createSharedAccessSignatureTokenProvider(connectionParams.getSharedAccessKeyName(), connectionParams.getSharedAccessKey());
		HybridConnectionListener listener = new HybridConnectionListener(new URI(connectionParams.getEndpoint().toString() + connectionParams.getEntityPath()), tokenProvider);
        
        listener.openAsync().join();
        System.out.println("Listener is online. Press ENTER to terminate this program.");
        
        CompletableFuture.runAsync(() -> {
        	Scanner in = new Scanner(System.in);
        	in.nextLine();
        	
    		listener.closeAsync().join();
    		in.close();
        });
        
        while (listener.isOnline()) {
        	ClientWebSocket websocket = listener.acceptConnectionAsync().join();
			
        	// If listener closes, then listener.acceptConnectionAsync() will complete with null after closing down
        	if (websocket != null) {
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
        	}
        }
	}
}
