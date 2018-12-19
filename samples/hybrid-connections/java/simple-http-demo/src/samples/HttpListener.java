package samples;

import java.io.IOException;
import java.net.URI;
import java.net.URISyntaxException;
import java.nio.ByteBuffer;
import java.util.Map;
import java.util.Scanner;
import java.util.concurrent.CompletableFuture;

import com.microsoft.azure.relay.HybridConnectionListener;
import com.microsoft.azure.relay.HybridConnectionUtil;
import com.microsoft.azure.relay.RelayedHttpListenerResponse;
import com.microsoft.azure.relay.TokenProvider;

public class HttpListener {
	static final String CONNECTION_STRING_ENV_VARIABLE_NAME = "RELAY_CONNECTION_STRING";
	static final Map<String, String> connectionParams = HybridConnectionUtil.parseConnectionString(System.getenv(CONNECTION_STRING_ENV_VARIABLE_NAME));
	static final String RELAY_NAMESPACE = connectionParams.get("Endpoint");
	static final String ENTITY_PATH = connectionParams.get("EntityPath");
	static final String KEY_NAME = connectionParams.get("SharedAccessKeyName");
	static final String KEY = connectionParams.get("SharedAccessKey");
	
	public static void main(String[] args) throws URISyntaxException {
		TokenProvider tokenProvider = TokenProvider.createSharedAccessSignatureTokenProvider(KEY_NAME, KEY);
		HybridConnectionListener listener = new HybridConnectionListener(new URI(RELAY_NAMESPACE + ENTITY_PATH), tokenProvider);
		
        CompletableFuture.runAsync(() -> {
        	Scanner in = new Scanner(System.in);
        	System.out.println("Press ENTER to termiate this program.");
        	String input = in.nextLine();
        	
        	if (input != null) {
        		listener.closeAsync().join();
        		in.close();
        	}
        });
        
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
            context.getResponse().close();
        });
        
        listener.openAsync().join();
	}
}
