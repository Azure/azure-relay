namespace Server
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Relay;
    using System.Net;

    public class Program
    {
        
        public static void Main(string[] args)
        {
            RunAsync(args).GetAwaiter().GetResult();
        }

        static async Task RunAsync(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("dotnet server [ns] [hc] [keyname] [key]");
                return;
            }
            
            var ns = args[0];
            var hc = args[1];
            var keyname = args[2];
            var key = args[3];

            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(keyname, key);
            var listener = new HybridConnectionListener(new Uri(string.Format("sb://{0}/{1}", ns, hc)), tokenProvider);
        
            // Subscribe to the status events.
            listener.Connecting += (o, e) => { Console.WriteLine("Connecting"); };
            listener.Offline += (o, e) => { Console.WriteLine("Offline"); };
            listener.Online += (o, e) => { Console.WriteLine("Online"); };
    
            // Provide an HTTP request handler
            listener.RequestHandler = (context) =>
            {
                // Do something with context.Request.Url, HttpMethod, Headers, InputStream...
                context.Response.StatusCode = HttpStatusCode.OK;
                context.Response.StatusDescription = "OK";
                using (var sw = new StreamWriter(context.Response.OutputStream))
                {
                    sw.WriteLine("hello!");
                }
                
                // The context MUST be closed here
                context.Response.Close();
            };
        
            // Opening the listener establishes the control channel to
            // the Azure Relay service. The control channel is continuously 
            // maintained, and is reestablished when connectivity is disrupted.
            await listener.OpenAsync();
            Console.WriteLine("Server listening");
        
            // Start a new thread that will continuously read the console.
            await Console.In.ReadLineAsync();
            
            // Close the listener
            await listener.CloseAsync();
        }
    }
}