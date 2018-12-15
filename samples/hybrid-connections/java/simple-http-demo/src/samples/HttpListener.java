package samples;

import java.io.IOException;
import java.net.URI;
import java.net.URISyntaxException;
import java.util.Map;

import com.microsoft.azure.relay.HybridConnectionListener;
import com.microsoft.azure.relay.RelayedHttpListenerContext;
import com.microsoft.azure.relay.RelayedHttpListenerResponse;
import com.microsoft.azure.relay.StringUtil;
import com.microsoft.azure.relay.TokenProvider;

public class HttpListener {
	static final String CONNECTION_STRING_ENV_VARIABLE_NAME = "RELAY_CONNECTION_STRING_ENVIRONMENT_VARIABLE";
	static final Map<String, String> connectionParams = StringUtil.parseConnectionString(System.getenv(CONNECTION_STRING_ENV_VARIABLE_NAME));
	static final String RELAY_NAME_SPACE = connectionParams.get("Endpoint");
	static final String CONNECTION_STRING = connectionParams.get("EntityPath");
	static final String KEY_NAME = connectionParams.get("SharedAccessKeyName");
	static final String KEY = connectionParams.get("SharedAccessKey");
	
	public static void main(String[] args) throws URISyntaxException {
		TokenProvider tokenProvider = TokenProvider.createSharedAccessSignatureTokenProvider(KEY_NAME, KEY);
		HybridConnectionListener listener = new HybridConnectionListener(new URI(RELAY_NAME_SPACE + CONNECTION_STRING), tokenProvider);
		
        listener.setRequestHandler((context) -> {
            RelayedHttpListenerResponse response = context.getResponse();
            response.setStatusCode(202);
            response.setStatusDescription("OK");
            
            String receivedText = (context.getRequest().getInputStream() != null) ? new String(context.getRequest().getInputStream().array()) : "";
            System.out.println("requestHandler received " + receivedText);
            
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
