using System.IO;
using System.Net;
using System.Threading.Tasks;
using System;
using Microsoft.Azure.Relay;

namespace Server
{
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
            listener.RequestHandler = async (context) =>
            {
                // Do something with context.Request.Url, HttpMethod, Headers, InputStream...
                Console.WriteLine();
                Console.WriteLine("=====HEADERS=====");
                Console.WriteLine(context.Request.Headers.ToString());
                Console.WriteLine("=====BODY=====");
                using (var sr = new StreamReader(context.Request.InputStream))
                {
                    Console.WriteLine(await sr.ReadToEndAsync());
                }

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
