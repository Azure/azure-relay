using System;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Relay.AspNetCore;
using Microsoft.Azure.Relay;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SelfHostServer
{
    public class Startup
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
            RunAsync(settings).GetAwaiter().GetResult();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Server options can be configured here instead of in Main.
            services.Configure<AzureRelayOptions>(options =>
            {
                
            });
        }

        public void Configure(IApplicationBuilder app)
        {
            app.Run(async context =>
            {
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync("Hello world from " + context.Request.Host + " at " + DateTime.Now);
            });
        }

        
        private static async Task RunAsync(Dictionary<string, string> settings)
        {
            var host = new WebHostBuilder()
                .ConfigureLogging(factory => factory.AddConsole())
                .UseStartup<Startup>()
                 .UseAzureRelay(options =>
                 {
                     options.UrlPrefixes.Add(
                         string.Format("https://{0}/{1}", settings["ns"], settings["path"]),
                         TokenProvider.CreateSharedAccessSignatureTokenProvider(settings["keyrule"], settings["key"]));
                 })
                .Build();

            host.Run();
        }
    }
}
