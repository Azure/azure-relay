// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if FEATURE_PENDING
using System;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Azure.Relay.AspNetCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.Relay.AspNetCore
{
    public class MessagePumpTests : IClassFixture<LaunchSettingsFixture>
    {
        private readonly LaunchSettingsFixture launchSettingsFixture;

        public MessagePumpTests(LaunchSettingsFixture launchSettingsFixture)
        {
            this.launchSettingsFixture = launchSettingsFixture;
        }

        [ConditionalFact]
        public void OverridingDirectConfigurationWithIServerAddressesFeatureSucceeds()
        {
            var serverAddress = new Uri(new Uri(Utilities.GetRelayUrl()), "1").AbsoluteUri;
            var overrideAddress = new Uri(new Uri(Utilities.GetRelayUrl()), "2").AbsoluteUri; 

            using (var server = Utilities.CreatePump())
            {
                var serverAddressesFeature = server.Features.Get<IServerAddressesFeature>();
                serverAddressesFeature.Addresses.Add(overrideAddress);
                serverAddressesFeature.PreferHostingUrls = true;
                
                server.StartAsync(new DummyApplication(), CancellationToken.None).Wait();

                Assert.Equal(overrideAddress, serverAddressesFeature.Addresses.Single());
            }
        }

        [ConditionalTheory]
        [InlineData("invalid address")]
        [InlineData("")]
        [InlineData(null)]
        public void DoesNotOverrideDirectConfigurationWithIServerAddressesFeature_IfPreferHostinUrlsFalse(string overrideAddress)
        {
            var serverAddress = Utilities.GetRelayUrl();

            using (var server = Utilities.CreatePump())
            {
                var serverAddressesFeature = server.Features.Get<IServerAddressesFeature>();
                serverAddressesFeature.Addresses.Add(overrideAddress);
                server.Listener.Options.UrlPrefixes.Add(serverAddress, Utilities.CreateTokenProvider());

                server.StartAsync(new DummyApplication(), CancellationToken.None).Wait();

                Assert.Equal(serverAddress, serverAddressesFeature.Addresses.Single());
            }
        }

        [ConditionalFact]
        public void DoesNotOverrideDirectConfigurationWithIServerAddressesFeature_IfAddressesIsEmpty()
        {
            var serverAddress = Utilities.GetRelayUrl();

            using (var server = Utilities.CreatePump())
            {
                var serverAddressesFeature = server.Features.Get<IServerAddressesFeature>();
                serverAddressesFeature.PreferHostingUrls = true;
                server.Listener.Options.UrlPrefixes.Add(AzureRelayUrlPrefix.Create(serverAddress, Utilities.CreateTokenProvider()));

                server.StartAsync(new DummyApplication(), CancellationToken.None).Wait();

                Assert.Equal(serverAddress, serverAddressesFeature.Addresses.Single());
            }
        }

        [ConditionalTheory]
        [InlineData("invalid address")]
        [InlineData("")]
        [InlineData(null)]
        public void OverridingIServerAdressesFeatureWithDirectConfiguration_WarnsOnStart(string serverAddress)
        {
            var overrideAddress = Utilities.GetRelayUrl();

            using (var server = Utilities.CreatePump())
            {
                var serverAddressesFeature = server.Features.Get<IServerAddressesFeature>();
                serverAddressesFeature.Addresses.Add(serverAddress);
                server.Listener.Options.UrlPrefixes.Add(AzureRelayUrlPrefix.Create(overrideAddress, Utilities.CreateTokenProvider()));

                server.StartAsync(new DummyApplication(), CancellationToken.None).Wait();

                Assert.Equal(overrideAddress, serverAddressesFeature.Addresses.Single());
            }
        }

        [ConditionalFact]
        public void UseIServerAdressesFeature_WhenNoDirectConfiguration()
        {
            var serverAddress = Utilities.GetRelayUrl();

            using (var server = Utilities.CreatePump())
            {
                var serverAddressesFeature = server.Features.Get<IServerAddressesFeature>();
                serverAddressesFeature.Addresses.Add(serverAddress);

                server.StartAsync(new DummyApplication(), CancellationToken.None).Wait();
            }
        }

        [ConditionalFact]
        public void UseDefaultAddress_WhenNoServerAddressAndNoDirectConfiguration()
        {
            using (var server = Utilities.CreatePump())
            {
                server.StartAsync(new DummyApplication(), CancellationToken.None).Wait();
            }
        }

    }
}
#endif