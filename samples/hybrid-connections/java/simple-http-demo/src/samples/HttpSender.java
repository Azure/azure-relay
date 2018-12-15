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

import com.microsoft.azure.relay.StringUtil;
import com.microsoft.azure.relay.TokenProvider;

public class HttpSender {
	static final String CONNECTION_STRING_ENV_VARIABLE_NAME = "RELAY_CONNECTION_STRING_ENVIRONMENT_VARIABLE";
	static final Map<String, String> connectionParams = StringUtil.parseConnectionString(System.getenv(CONNECTION_STRING_ENV_VARIABLE_NAME));
	static final String RELAY_NAME_SPACE = connectionParams.get("Endpoint");
	static final String CONNECTION_STRING = connectionParams.get("EntityPath");
	static final String KEY_NAME = connectionParams.get("SharedAccessKeyName");
	static final String KEY = connectionParams.get("SharedAccessKey");
	
	public static void main(String[] args) throws IOException, InterruptedException, ExecutionException {
		// Build a large test string over 64kb
		StringBuilder builder = new StringBuilder();
		String alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
		for (int i = 0; i < 2000; i++) {
			builder.append(alphabet);
		}
		String largeStr = builder.toString();
		Scanner in = new Scanner(System.in);
		
		TokenProvider tokenProvider = TokenProvider.createSharedAccessSignatureTokenProvider(KEY_NAME, KEY);
		StringBuilder urlBuilder = new StringBuilder(RELAY_NAME_SPACE + CONNECTION_STRING);
		urlBuilder.replace(0, 5, "https://");
		URL url = new URL(urlBuilder.toString());
		String tokenString = tokenProvider.getTokenAsync(url.toString(), Duration.ofHours(1)).join().getToken();
		
		while (true) {
			System.out.println("Please enter the message you want to send over http:");
			String message = in.nextLine();
			if (message.equals("quit")) {
				break;
			}
			
			HttpURLConnection conn = (HttpURLConnection)url.openConnection();
			// Non-empty messages must use POST
			conn.setRequestMethod(StringUtil.isNullOrEmpty(message) ? "GET" : "POST");
			conn.setRequestProperty("ServiceBusAuthorization", tokenString);
			conn.setDoOutput(true);
			
			OutputStreamWriter out = new OutputStreamWriter(conn.getOutputStream());
			out.write(message, 0, message.length());
			out.flush();
			out.close();
			
			String inputLine;
			StringBuilder builder2 = new StringBuilder();
			BufferedReader inStream = new BufferedReader(new InputStreamReader(conn.getInputStream()));
			
			System.out.println("status code: " + conn.getResponseCode());
			while ((inputLine = inStream.readLine()) != null) {
				builder2.append(inputLine);
			}
			inStream.close();
			System.out.println("received back " + builder2.toString());
		}
		in.close();
	}
}
