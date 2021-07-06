using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Relay;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            RunAsync(args).GetAwaiter().GetResult();
        }

        static async Task RunAsync(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("dotnet client [ns] [hc] [keyname] [key]");
                return;
            }

            var ns = args[0];
            var hc = args[1];
            var keyname = args[2];
            var key = args[3];

            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(keyname, key);
            var uri = new Uri(string.Format("https://{0}/{1}", ns, hc));
            var token = (await tokenProvider.GetTokenAsync(uri.AbsoluteUri, TimeSpan.FromHours(1))).TokenString;
            var client = new HttpClient();
            var request = new HttpRequestMessage()
            {
                RequestUri = uri,
                Method = HttpMethod.Get,
            };
            request.Headers.Add("ServiceBusAuthorization", token);
            var response = await client.SendAsync(request);
            Console.WriteLine(await response.Content.ReadAsStringAsync());
        }
    }
}