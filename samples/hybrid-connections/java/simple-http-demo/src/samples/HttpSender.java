package samples;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.OutputStreamWriter;
import java.net.HttpURLConnection;
import java.net.URL;
import java.time.Duration;
import java.util.Map;
import java.util.Scanner;
import java.util.concurrent.ExecutionException;

import com.microsoft.azure.relay.HybridConnectionUtil;
import com.microsoft.azure.relay.StringUtil;
import com.microsoft.azure.relay.TokenProvider;

public class HttpSender {
	static final String CONNECTION_STRING_ENV_VARIABLE_NAME = "RELAY_CONNECTION_STRING";
	static final Map<String, String> connectionParams = HybridConnectionUtil.parseConnectionString(System.getenv(CONNECTION_STRING_ENV_VARIABLE_NAME));
	static final String RELAY_NAMESPACE = connectionParams.get("Endpoint");
	static final String ENTITY_PATH = connectionParams.get("EntityPath");
	static final String KEY_NAME = connectionParams.get("SharedAccessKeyName");
	static final String KEY = connectionParams.get("SharedAccessKey");
	
	public static void main(String[] args) throws IOException, InterruptedException, ExecutionException {
		TokenProvider tokenProvider = TokenProvider.createSharedAccessSignatureTokenProvider(KEY_NAME, KEY);
		String urlString = HybridConnectionUtil.getURLString(RELAY_NAMESPACE, ENTITY_PATH);
		String tokenString = tokenProvider.getTokenAsync(urlString, Duration.ofHours(1)).join().getToken();
		Scanner in = new Scanner(System.in);

		while (true) {
			System.out.println("Please enter the message you want to send over http:");
			String message = in.nextLine();
			if (message.equalsIgnoreCase("quit") || message.equalsIgnoreCase("q")) break;
			
			HttpURLConnection conn = (HttpURLConnection)new URL(urlString).openConnection();
			// To send a message body, use POST
			conn.setRequestMethod(StringUtil.isNullOrEmpty(message) ? "GET" : "POST");
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
