//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Azure.Relay;
using Microsoft.Identity.Client;

namespace RoleBasedAccessControl
{
    class Program
    {
        enum RbacAuthenticationOption
        {
            ManagedIdentity,
            UserAssignedIdentity,
            AAD
        }

        static async Task Main(string[] args)
        {
            string hostAddress;
            string hybridConnectionName;
            string clientId = null;
            string tenantId = null;
            string clientSecret = null;
            RbacAuthenticationOption option;

            if (args.Length == 2)
            {
                option = RbacAuthenticationOption.ManagedIdentity;
                hostAddress = args[0];
                hybridConnectionName = args[1];
            }
            else if (args.Length == 3)
            {
                option = RbacAuthenticationOption.UserAssignedIdentity;
                hostAddress = args[0];
                hybridConnectionName = args[1];
                clientId = args[2];
            }
            else if (args.Length == 5)
            {
                option = RbacAuthenticationOption.AAD;
                hostAddress = args[0];
                hybridConnectionName = args[1];
                clientId = args[2];
                tenantId = args[3];
                clientSecret = args[4];
            }
            else
            {
                Console.WriteLine("Please run with parameters of the following format for the corresponding RBAC authentication method:");
                Console.WriteLine("System Managed Identity: [HostAddress] [HybridConnectionName]");
                Console.WriteLine("User Assigned Identity: [HostAddress] [HybridConnectionName] [ClientId]");
                Console.WriteLine("Azure Active Directory:  [HostAddress] [HybridConnectionName] [ClientId] [TenantId] [ClientSecret]");
                Console.WriteLine("Press <Enter> to exit...");
                Console.ReadLine();
                return;
            }

            TokenProvider tokenProvider = null;
            switch (option)
            {
                case RbacAuthenticationOption.ManagedIdentity:
                    tokenProvider = TokenProvider.CreateManagedIdentityTokenProvider();
                    break;
                case RbacAuthenticationOption.UserAssignedIdentity:
                    var managedCredential = new ManagedIdentityCredential(clientId);
                    tokenProvider = TokenProvider.CreateManagedIdentityTokenProvider(managedCredential);
                    break;
                case RbacAuthenticationOption.AAD:
                    tokenProvider = GetAadTokenProvider(clientId, tenantId, clientSecret);
                    break;
            }

            var hybridConnectionUri = new Uri($"{hostAddress}/{hybridConnectionName}");
            HybridConnectionListener listener = null;
            try
            {
                // The HybridConnection should be already created through Azure Portal or other means
                Console.WriteLine($"Creating the Relay listener instance with RBAC option: {option}");
                listener = new HybridConnectionListener(hybridConnectionUri, tokenProvider);

                await listener.OpenAsync(TimeSpan.FromSeconds(10));
                Console.WriteLine("Created and connected the Relay listener instance.");

                Console.WriteLine($"Creating the Relay sender instance with RBAC option: {option}");
                var sender = new HybridConnectionClient(hybridConnectionUri, tokenProvider);
                var createSenderTask = sender.CreateConnectionAsync();
                var listenerAcceptTask = listener.AcceptConnectionAsync();
                using (HybridConnectionStream senderStream = await createSenderTask)
                using (HybridConnectionStream listenerStream = await listenerAcceptTask)
                {
                    Console.WriteLine("Created and connected the Relay sender instance.");
                    var senderCloseTask = senderStream.CloseAsync(CancellationToken.None);
                    await listenerStream.CloseAsync(CancellationToken.None);
                    await senderCloseTask;
                }

                // Configure a RequestHandler for HTTP request/response mode
                listener.RequestHandler = (context) =>
                {
                    context.Response.StatusCode = HttpStatusCode.OK;
                    using (var sw = new StreamWriter(context.Response.OutputStream))
                    {
                        sw.WriteLine("hello!");
                    }

                    // The context must be closed to complete sending the response
                    context.Response.Close();
                };

                Console.WriteLine($"Sending a HTTP request by setting the token in request header with RBAC option: {option}");
                SecurityToken token = await tokenProvider.GetTokenAsync(hybridConnectionUri.AbsoluteUri, TimeSpan.FromMinutes(30));
                var request = new HttpRequestMessage();
                request.Headers.Add(HttpRequestHeader.Authorization.ToString(), token.TokenString);
                var requestUri = new UriBuilder(hybridConnectionUri) { Scheme = "https" }.Uri;
                using (HttpClient client = new HttpClient { BaseAddress = requestUri })
                {
                    using (var response = await client.SendAsync(request))
                    {
                        Console.WriteLine($"Response status code: {response.StatusCode}. Response reason phrase: {response.ReasonPhrase}");
                    }
                }

                Console.WriteLine($"Sending a HTTP request by setting the token in query string with RBAC option: {option}");
                token = await tokenProvider.GetTokenAsync(hybridConnectionUri.AbsoluteUri, TimeSpan.FromMinutes(30));
                request = new HttpRequestMessage();
                requestUri = new UriBuilder(requestUri) { Query = $"?sb-hc-token={token.TokenString}" }.Uri;
                using (HttpClient client = new HttpClient { BaseAddress = requestUri })
                {
                    using (var response = await client.SendAsync(request))
                    {
                        Console.WriteLine($"Response status code: {response.StatusCode}. Response reason phrase: {response.ReasonPhrase}");
                    }
                }

                Console.WriteLine("Press <Enter> to exit...");
                Console.ReadLine();
            }
            finally
            {
                if (listener != null)
                {
                    await listener.CloseAsync();
                }
            }
        }

        static TokenProvider GetAadTokenProvider(string clientId, string tenantId, string clientSecret)
        {
            return TokenProvider.CreateAzureActiveDirectoryTokenProvider(
                async (audience, authority, state) =>
                {
                    IConfidentialClientApplication app = ConfidentialClientApplicationBuilder.Create(clientId)
                        .WithAuthority(authority)
                        .WithClientSecret(clientSecret)
                        .Build();

                    var authResult = await app.AcquireTokenForClient(new [] { $"{audience}/.default" }).ExecuteAsync();
                    return authResult.AccessToken;
                },
                $"https://login.microsoftonline.com/{tenantId}");
        }
    }
}
