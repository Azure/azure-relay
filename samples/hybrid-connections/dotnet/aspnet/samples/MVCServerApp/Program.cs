using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.Relay;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MVCServerApp
{
    public class Program
    {
        static Dictionary<string, string> settings = new Dictionary<string, string>(){
            {"ns" , Environment.GetEnvironmentVariable("SB_HC_NAMESPACE")},
            {"path", Environment.GetEnvironmentVariable("SB_HC_PATH") },
            {"keyrule", Environment.GetEnvironmentVariable("SB_HC_KEYRULE") },
            {"key", Environment.GetEnvironmentVariable("SB_HC_KEY")}
        };


        public static void Main(string[] args)
        {
            foreach (var arg in args)
            {
                var match = Regex.Match(arg, "^ --(.*?)(?:= (.*)) ?$");
                if (match.Success)
                {
                    settings[match.Captures[0].Value] = match.Captures.Count > 1 ? match.Captures[1].Value : "true";
                }
            }

            if (string.IsNullOrEmpty(settings["ns"]) ||
                string.IsNullOrEmpty(settings["path"]) ||
                string.IsNullOrEmpty(settings["keyrule"]) ||
                string.IsNullOrEmpty(settings["key"]))
            {
                Console.WriteLine("Required arguments:\n--ns=[namespace] --path=[path] --keyrule=[keyrule] --key=[key]");
                return;
            }

            BuildWebHost(settings).Run();
        }

        public static IWebHost BuildWebHost(Dictionary<string, string> settings) =>
            new WebHostBuilder()
                .ConfigureLogging(factory => { factory.AddConsole(); factory.AddDebug(); })
                .UseStartup<Startup>()
                .UseAzureRelay(options =>
                {
                    options.UrlPrefixes.Add(
                        string.Format("https://{0}/{1}", settings["ns"], settings["path"]),
                        TokenProvider.CreateSharedAccessSignatureTokenProvider(settings["keyrule"], settings["key"]));
                })
                .UseContentRoot(Path.GetFullPath(@"."))
                .UseWebRoot(Path.GetFullPath(@".\wwwroot"))
                .Build();
    }
}
