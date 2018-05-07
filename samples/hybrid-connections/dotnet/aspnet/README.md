AzureRelayServer
=================

This repo contains a web server for ASP.NET Core based on Azure Relay Hybrid Connections HTTP.

The integration supports most ASP.NET scenarios, with a few exceptions. WebSocket support will
be added in the near future, for instance.

```

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
```

