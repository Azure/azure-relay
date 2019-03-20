import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.Reader;
import java.net.URI;
import java.net.URISyntaxException;
import java.util.Scanner;

import com.microsoft.azure.relay.HybridConnectionListener;
import com.microsoft.azure.relay.RelayConnectionStringBuilder;
import com.microsoft.azure.relay.RelayedHttpListenerResponse;
import com.microsoft.azure.relay.TokenProvider;

public class HttpListener {
	static final String CONNECTION_STRING_ENV_VARIABLE_NAME = "RELAY_CONNECTION_STRING";
	static final RelayConnectionStringBuilder connectionParams = new RelayConnectionStringBuilder(
			System.getenv(CONNECTION_STRING_ENV_VARIABLE_NAME));

	public static void main(String[] args) throws URISyntaxException {
		TokenProvider tokenProvider = TokenProvider.createSharedAccessSignatureTokenProvider(
				connectionParams.getSharedAccessKeyName(),
				connectionParams.getSharedAccessKey());
		HybridConnectionListener listener = new HybridConnectionListener(
				new URI(connectionParams.getEndpoint().toString() + connectionParams.getEntityPath()),
				tokenProvider);

		// The "context" object encapsulates both the incoming request and the outgoing response
		listener.setRequestHandler((context) -> {
			String receivedText = "";
			if (context.getRequest().getInputStream() != null) {
				try (Reader reader = new BufferedReader(new InputStreamReader(context.getRequest().getInputStream(), "UTF8"))) {
					StringBuilder builder = new StringBuilder();
					int c = 0;
					while ((c = reader.read()) != -1) {
						builder.append((char) c);
					}
					receivedText = builder.toString();
				} catch (IOException e) {
					System.out.println(e.getMessage());
				}
			}
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

		listener.close();
		in.close();
	}
}
