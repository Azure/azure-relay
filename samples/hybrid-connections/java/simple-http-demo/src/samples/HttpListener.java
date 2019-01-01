package samples;

import java.io.IOException;
import java.net.URI;
import java.net.URISyntaxException;
import java.nio.ByteBuffer;
import java.util.Scanner;

import com.microsoft.azure.relay.HybridConnectionListener;
import com.microsoft.azure.relay.RelayConnectionStringBuilder;
import com.microsoft.azure.relay.RelayedHttpListenerResponse;
import com.microsoft.azure.relay.TokenProvider;

public class HttpListener {
	static final String CONNECTION_STRING_ENV_VARIABLE_NAME = "RELAY_CONNECTION_STRING";
	static final RelayConnectionStringBuilder connectionParams = new RelayConnectionStringBuilder(System.getenv(CONNECTION_STRING_ENV_VARIABLE_NAME));
	
	public static void main(String[] args) throws URISyntaxException {
		TokenProvider tokenProvider = TokenProvider.createSharedAccessSignatureTokenProvider(connectionParams.getSharedAccessKeyName(), connectionParams.getSharedAccessKey());
		HybridConnectionListener listener = new HybridConnectionListener(new URI(connectionParams.getEndpoint().toString() + connectionParams.getEntityPath()), tokenProvider);
        
        listener.setRequestHandler((context) -> {
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

            // The context MUST be closed for the message to be sent
            response.close();
        });
        
        listener.openAsync().join();
        
    	Scanner in = new Scanner(System.in);
    	System.out.println("Press ENTER to terminate this program.");
    	in.nextLine();

    	listener.closeAsync().join();
    	in.close();
	}
}
