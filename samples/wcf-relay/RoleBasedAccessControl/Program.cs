//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using System.Threading.Tasks;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Identity.Client;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace RoleBasedAccessControl
{
    class Program
    {
        static readonly Binding[] RelayBindings = new Binding[]
        {
            new BasicHttpRelayBinding(EndToEndBasicHttpSecurityMode.Transport, RelayClientAuthenticationType.RelayAccessToken) { IsDynamic = false },
            new NetEventRelayBinding(EndToEndSecurityMode.Transport, RelayEventSubscriberAuthenticationType.RelayAccessToken),
            new NetOnewayRelayBinding(EndToEndSecurityMode.Transport, RelayClientAuthenticationType.RelayAccessToken),
            new NetTcpRelayBinding() { IsDynamic = false },
            new WebHttpRelayBinding(EndToEndWebHttpSecurityMode.Transport, RelayClientAuthenticationType.RelayAccessToken) { IsDynamic = false },
            new WS2007HttpRelayBinding(EndToEndSecurityMode.Transport, RelayClientAuthenticationType.RelayAccessToken) { IsDynamic = false }
        };

        enum RbacAuthenticationOption
        {
            ManagedIdentity,
            UserAssignedIdentity,
            AAD
        }

        static async Task Main(string[] args)
        {
            string hostAddress;
            string wcfRelayName;
            string clientId = null;
            string tenantId = null;
            string clientSecret = null;
            RbacAuthenticationOption option;

            if (args.Length == 2)
            {
                option = RbacAuthenticationOption.ManagedIdentity;
                hostAddress = args[0];
                wcfRelayName = args[1];
            }
            else if (args.Length == 3)
            {
                option = RbacAuthenticationOption.UserAssignedIdentity;
                hostAddress = args[0];
                wcfRelayName = args[1];
                clientId = args[2];
            }
            else if (args.Length == 5)
            {
                option = RbacAuthenticationOption.AAD;
                hostAddress = args[0];
                wcfRelayName = args[1];
                clientId = args[2];
                tenantId = args[3];
                clientSecret = args[4];
            }
            else
            {
                Console.WriteLine("Please run with parameters of the following format for the corresponding RBAC authentication method:");
                Console.WriteLine("System Managed Identity: [HostAddress] [WCFRelayName]");
                Console.WriteLine("User Assigned Identity: [HostAddress] [WCFRelayName] [ClientId]");
                Console.WriteLine("Azure Active Directory:  [HostAddress] [WCFRelayName] [ClientId] [TenantId] [ClientSecret]");
                Console.WriteLine("Press <Enter> to exit...");
                Console.ReadLine();
                return;
            }

            TokenProvider tokenProvider = null;
            switch (option)
            {
                case RbacAuthenticationOption.ManagedIdentity:
                    tokenProvider = TokenProvider.CreateManagedIdentityTokenProvider(new Uri("https://relay.azure.net/"));
                    break;
                case RbacAuthenticationOption.UserAssignedIdentity:
                    var azureServiceTokenProvider = new AzureServiceTokenProvider($"RunAs=App;AppId={clientId}");
                    tokenProvider = TokenProvider.CreateManagedIdentityTokenProvider(azureServiceTokenProvider, new Uri("https://relay.azure.net/"));
                    break;
                case RbacAuthenticationOption.AAD:
                    tokenProvider = GetAadTokenProvider(clientId, tenantId, clientSecret);
                    break;
            }

            foreach (Binding binding in RelayBindings)
            {
                Console.WriteLine($"=====Running the WCF sample with binding: {binding.GetType()}=====");
                await RunWcfSampleAsync(hostAddress, wcfRelayName, tokenProvider, binding);
            }

            Console.WriteLine("Press <Enter> to exit...");
            Console.ReadLine();
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
                new Uri("https://relay.azure.net/"),
                $"https://login.microsoftonline.com/{tenantId}");
        }

        /***********************************************************************
        * The code below is not specific to Role Based Access Control. 
        * Once the TokenProvider instance is created, they can work with either RBAC or SAS
        ***********************************************************************/

        static async Task RunWcfSampleAsync(string hostAddress, string wcfRelayName, TokenProvider tokenProvider, Binding binding)
        {
            var namespaceManager = new NamespaceManager(hostAddress, tokenProvider);
            ServiceHost serviceHost = null;
            RelayDescription relayDescription = null;
            bool deleted = false;

            UriBuilder uriBuilder = new UriBuilder(hostAddress);
            uriBuilder.Scheme = binding.Scheme;
            uriBuilder.Path = wcfRelayName;
            Uri relayAddress = uriBuilder.Uri;

            try
            {
                // Cannot create NetOneWay and NetEvent (extends NetOneway) Relays, they must be created dynamically
                if (!(binding is NetOnewayRelayBinding))
                {
                    Console.WriteLine("Creating the WCF Relay...");
                    relayDescription = new RelayDescription(wcfRelayName, GetRelayType(binding));
                    namespaceManager.CreateRelay(relayDescription);
                    Console.WriteLine($"Created the WCF Relay: {wcfRelayName}");
                }

                Console.WriteLine("Creating and opening the listener for the WCF Relay...");
                ServiceEndpoint endpoint = null;
                if (binding is NetTcpRelayBinding || binding is BasicHttpRelayBinding || binding is WSHttpRelayBinding)
                {
                    serviceHost = new ServiceHost(typeof(MathService), relayAddress);
                    endpoint = serviceHost.AddServiceEndpoint(typeof(IMathService), binding, string.Empty);
                }
                else if (binding is WebHttpRelayBinding)
                {
                    serviceHost = new WebServiceHost(typeof(WebHttpService), relayAddress);
                    endpoint = serviceHost.AddServiceEndpoint(typeof(IWebRequestResponse), binding, string.Empty);
                    endpoint.Behaviors.Add(new WebHttpBehavior());
                }
                else if (binding is NetOnewayRelayBinding)
                {
                    serviceHost = new ServiceHost(new NotificationService(), relayAddress);
                    endpoint = serviceHost.AddServiceEndpoint(typeof(INotificationService), binding, string.Empty);
                }

                endpoint.EndpointBehaviors.Add(new TransportClientEndpointBehavior(tokenProvider));
                DisableHttpHelpPage(serviceHost);
                serviceHost.Open();
                Console.WriteLine($"The listener to {wcfRelayName} was sucessfully opened");

                Console.WriteLine($"Creating and sending with a channel to WCF Relay...");
                CreateAndSendWithChannel(relayAddress, tokenProvider, binding);
                Console.WriteLine($"Created and sent with channel to WCF Relay {wcfRelayName}");

                if (!(binding is NetOnewayRelayBinding))
                {
                    Console.WriteLine("Getting the WCF Relay...");
                    await namespaceManager.GetRelayAsync(relayDescription.Path);
                    Console.WriteLine($"Got WCF Relay {relayDescription.Path}");

                    Console.WriteLine("Updating the WCF Relay...");
                    await namespaceManager.UpdateRelayAsync(relayDescription);
                    Console.WriteLine($"Updated the WCF Relay {relayDescription.Path}");

                    Console.WriteLine("Deleting the WCF Relay...");
                    await namespaceManager.DeleteRelayAsync(relayDescription.Path);
                    Console.WriteLine($"Deleted the WCF Relay {relayDescription.Path}");
                    deleted = true;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: {e}");
            }
            finally
            {
                SafeClose(serviceHost);
                if (!deleted && relayDescription != null)
                {
                    await namespaceManager.DeleteRelayAsync(relayDescription.Path);
                }
            }
        }

        static void CreateAndSendWithChannel(Uri address, TokenProvider tokenProvider, Binding binding)
        {
            if (binding is NetTcpRelayBinding || binding is BasicHttpRelayBinding || binding is WSHttpRelayBinding)
            {
                CreateAndSendWithMathChannel(address, tokenProvider, binding);
            }
            else if (binding is NetOnewayRelayBinding)
            {
                // Includes NetEvent as well
                CreateAndSendWithNotificationChannel(address, tokenProvider, binding);
            }
            else if (binding is WebHttpBinding)
            {
                CreateAndSendWithWebRequestResponseChannel(address, tokenProvider, binding);
            }
        }

        static void CreateAndSendWithMathChannel(Uri address, TokenProvider tokenProvider, Binding binding)
        {
            ChannelFactory<IMathClient> channelFactory = null;
            IMathClient client = null;

            try
            {
                channelFactory = new ChannelFactory<IMathClient>(binding, address.AbsoluteUri);
                channelFactory.Endpoint.Behaviors.Add(new TransportClientEndpointBehavior(tokenProvider));
                client = channelFactory.CreateChannel();
                client.Add(1, 1);
            }
            finally
            {
                SafeClose(client);
                SafeClose(channelFactory);
            }
        }

        static void CreateAndSendWithNotificationChannel(Uri address, TokenProvider tokenProvider, Binding binding)
        {
            ChannelFactory<INotificationService> channelFactory = null;
            INotificationService channel = null;

            try
            {
                channelFactory = new ChannelFactory<INotificationService>(binding, address.AbsoluteUri);
                channelFactory.Endpoint.Behaviors.Add(new TransportClientEndpointBehavior(tokenProvider));
                channel = channelFactory.CreateChannel();
                channel.Notify(Guid.NewGuid().ToString(), "My event");
            }
            finally
            {
                SafeClose((ICommunicationObject)channel);
                SafeClose(channelFactory);
            }
        }

        static void CreateAndSendWithWebRequestResponseChannel(Uri address, TokenProvider tokenProvider, Binding binding)
        {
            var channelFactory = new ChannelFactory<IWebRequestResponseChannel>(binding, new EndpointAddress(address));
            IWebRequestResponseChannel client = null;

            try
            {
                channelFactory.Endpoint.Behaviors.Add(new TransportClientEndpointBehavior(tokenProvider));
                channelFactory.Endpoint.Behaviors.Add(new WebHttpBehavior());
                client = channelFactory.CreateChannel();
                string getResponse = client.SimpleGetString();
                string postResponse = client.SimplePostString("my string");
                string getArgsResponse = client.GetWithUriArgs("arg1", "arg2");
            }
            finally
            {
                SafeClose(client);
                SafeClose(channelFactory);
            }
        }

        static RelayType GetRelayType(Binding binding)
        {
            if (binding is NetTcpRelayBinding)
            {
                return RelayType.NetTcp;
            }
            else if (binding is NetOnewayRelayBinding)
            {
                return RelayType.NetOneway;
            }
            else if (binding is NetEventRelayBinding)
            {
                return RelayType.NetEvent;
            }
            else if (binding is BasicHttpRelayBinding || binding is WebHttpRelayBinding || binding is WSHttpRelayBinding)
            {
                return RelayType.Http;
            }

            throw new ArgumentException($"Unrecognized Relay binding type: {binding.GetType()}");
        }

        static void SafeClose(ICommunicationObject communicationObject)
        {
            if (communicationObject == null || communicationObject.State == CommunicationState.Closed)
            {
                return;
            }

            try
            {
                if (communicationObject.State != CommunicationState.Faulted)
                {
                    communicationObject.Close();
                    return;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error closing {communicationObject.GetType()}: {e}");
            }

            communicationObject.Abort();
        }

        static void DisableHttpHelpPage(ServiceHost serviceHost)
        {
            var debugBehavior = serviceHost.Description.Behaviors.Find<ServiceDebugBehavior>();
            if (debugBehavior != null)
            {
                debugBehavior.HttpHelpPageEnabled = false;
                debugBehavior.HttpsHelpPageEnabled = false;
            }
        }
    }
}
