import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.OutputStreamWriter;
import java.net.HttpURLConnection;
import java.net.URL;
import java.time.Duration;
import java.util.Scanner;
import java.util.concurrent.ExecutionException;

import com.microsoft.azure.relay.RelayConnectionStringBuilder;
import com.microsoft.azure.relay.TokenProvider;

public class HttpSender {
	static final String CONNECTION_STRING_ENV_VARIABLE_NAME = "RELAY_CONNECTION_STRING";
	static final RelayConnectionStringBuilder connectionParams = new RelayConnectionStringBuilder(System.getenv(CONNECTION_STRING_ENV_VARIABLE_NAME));
	
	public static void main(String[] args) throws IOException, InterruptedException, ExecutionException {
		TokenProvider tokenProvider = TokenProvider.createSharedAccessSignatureTokenProvider(connectionParams.getSharedAccessKeyName(), connectionParams.getSharedAccessKey());
		
		// For HTTP connections, the scheme must be https://
		StringBuilder urlBuilder = new StringBuilder(connectionParams.getEndpoint().toString() + connectionParams.getEntityPath());
		urlBuilder.replace(0, 5, "https://");
		URL url = new URL(urlBuilder.toString());
		String tokenString = tokenProvider.getTokenAsync(url.toString(), Duration.ofHours(1)).join().getToken();
		Scanner in = new Scanner(System.in);

		while (true) {
			System.out.println("Please enter the message you want to send over http, \"quit\" or \"q\" to terminate:");
			String message = in.nextLine();
			if (message.equalsIgnoreCase("quit") || message.equalsIgnoreCase("q")) break;
			
			HttpURLConnection conn = (HttpURLConnection) url.openConnection();
			// To send a message body, use POST
			conn.setRequestMethod((message == null || message.length() == 0) ? "GET" : "POST");
			conn.setRequestProperty("ServiceBusAuthorization", tokenString);
			conn.setDoOutput(true);
			
			OutputStreamWriter out = new OutputStreamWriter(conn.getOutputStream());
			out.write(message, 0, message.length());
			out.flush();
			out.close();
			
			String inputLine;
			StringBuilder responseBuilder = new StringBuilder();
			BufferedReader inStream = new BufferedReader(new InputStreamReader(conn.getInputStream()));
			
			System.out.println("status code: " + conn.getResponseCode());
			while ((inputLine = inStream.readLine()) != null) {
				responseBuilder.append(inputLine);
			}
			
			inStream.close();
			System.out.println("received back " + responseBuilder.toString());
		}
		
		in.close();
	}
}