import java.net.URI;
import java.net.URISyntaxException;
import java.nio.ByteBuffer;
import java.util.Scanner;
import java.util.concurrent.CompletableFuture;
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
        	
    		listener.close();
    		in.close();
        });
        
        while (listener.isOnline()) {
        	listener.acceptConnectionAsync().thenAccept(connection -> {
            	// connection may be null if the listener is closed before receiving a connection
            	if (connection != null) {
                	System.out.println("New session connected.");
                	
        			while (connection.isOpen()) {
        				ByteBuffer bytesReceived = connection.readAsync().join();
        				String msg = new String(bytesReceived.array());
        				ByteBuffer msgToSend = ByteBuffer.wrap(("Echo: " + msg).getBytes());

        				System.out.println("Received: " + msg);
        				connection.writeAsync(msgToSend);
        			}
        			System.out.println("Session disconnected.");
            	}
        	}).join();
			
        	// If listener closes, then listener.acceptConnectionAsync() will complete with null after closing down
        }
	}
}
