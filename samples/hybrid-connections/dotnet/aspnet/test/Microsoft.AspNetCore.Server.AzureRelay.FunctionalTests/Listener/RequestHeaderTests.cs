// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if FEATURE_PENDING

using System;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Microsoft.Azure.Relay.AspNetCore.Listener
{
    public class RequestHeaderTests : IClassFixture<LaunchSettingsFixture>
    {
        private readonly LaunchSettingsFixture launchSettingsFixture;

        public RequestHeaderTests(LaunchSettingsFixture launchSettingsFixture)
        {
            this.launchSettingsFixture = launchSettingsFixture;
        }

        [ConditionalFact]
        public async Task RequestHeaders_ClientSendsDefaultHeaders_Success()
        {
            string address;
            using (var server = Utilities.CreateHttpServer(out address))
            {
                Task<string> responseTask = SendRequestAsync(address);

                var context = await server.AcceptAsync(Utilities.DefaultTimeout);
                var requestHeaders = context.Request.Headers;
                // NOTE: The System.Net client only sends the Connection: keep-alive header on the first connection per service-point.
                // Assert.Equal(2, requestHeaders.Count);
                // Assert.Equal("Keep-Alive", requestHeaders.Get("Connection"));
                Assert.Equal(new Uri(address).Authority, requestHeaders["Host"]);
                StringValues values;
                Assert.False(requestHeaders.TryGetValue("Accept", out values));
                Assert.False(requestHeaders.ContainsKey("Accept"));
                Assert.True(StringValues.IsNullOrEmpty(requestHeaders["Accept"]));
                context.Dispose();

                string response = await responseTask;
                Assert.Equal(string.Empty, response);
            }
        }

        [ConditionalFact]
        public async Task RequestHeaders_ClientSendsCustomHeaders_Success()
        {
            string address;
            using (var server = Utilities.CreateHttpServer(out address))
            {
                string[] customValues = new string[] { "custom1, and custom2", "custom3" };
                Task responseTask = SendRequestAsync(address, "Custom-Header", customValues);

                var context = await server.AcceptAsync(Utilities.DefaultTimeout);
                var requestHeaders = context.Request.Headers;
                Assert.Equal(3, requestHeaders.Count);
                Assert.Equal(new Uri(address).Authority, requestHeaders["Host"]);
                Assert.Equal(new[] { new Uri(address).Authority }, requestHeaders.GetValues("Host"));
                // Apparently Http.Sys squashes request headers together.
                Assert.Equal("custom1, and custom2, custom3", requestHeaders["Custom-Header"]);
                Assert.Equal(new[] { "custom1", "and custom2", "custom3" }, requestHeaders.GetValues("Custom-Header"));
                Assert.Equal("spacervalue, spacervalue", requestHeaders["Spacer-Header"]);
                Assert.Equal(new[] { "spacervalue", "spacervalue" }, requestHeaders.GetValues("Spacer-Header"));
                context.Dispose();

                await responseTask;
            }
        }

#if FEATURE_PENDING
        [ConditionalFact]
        public async Task RequestHeaders_ClientSendsUtf8Headers_Success()
        {
            string address;
            using (var server = Utilities.CreateHttpServer(out address))
            {
                string[] customValues = new string[] { "custom1, and custom测试2", "custom3" };
                Task responseTask = SendRequestAsync(address, "Custom-Header", customValues);

                var context = await server.AcceptAsync(Utilities.DefaultTimeout);
                var requestHeaders = context.Request.Headers;
                Assert.Equal(3, requestHeaders.Count);
                Assert.Equal(new Uri(address).Authority, requestHeaders["Host"]);
                Assert.Equal(new[] { new Uri(address).Authority }, requestHeaders.GetValues("Host"));
                // Apparently Http.Sys squashes request headers together.
                Assert.Equal("custom1, and custom测试2, custom3", requestHeaders["Custom-Header"]);
                Assert.Equal(new[] { "custom1", "and custom测试2", "custom3" }, requestHeaders.GetValues("Custom-Header"));
                Assert.Equal("spacervalue, spacervalue", requestHeaders["Spacer-Header"]);
                Assert.Equal(new[] { "spacervalue", "spacervalue" }, requestHeaders.GetValues("Spacer-Header"));
                context.Dispose();

                await responseTask;
            }
        }
#endif

#if FEATURE_PENDING
        [ConditionalFact]
        public async Task RequestHeaders_ClientSendsKnownHeaderWithNoValue_Success()
        {
            string address;
            using (var server = Utilities.CreateHttpServer(out address))
            {
                string[] customValues = new string[] { "" };
                Task responseTask = SendRequestAsync(address, "If-None-Match", customValues);

                var context = await server.AcceptAsync(Utilities.DefaultTimeout);
                var requestHeaders = context.Request.Headers;
                Assert.Equal(3, requestHeaders.Count);
                Assert.Equal(new Uri(address).Authority, requestHeaders["Host"]);
                Assert.Equal(new[] { new Uri(address).Authority }, requestHeaders.GetValues("Host"));
                Assert.Equal(StringValues.Empty, requestHeaders["If-None-Match"]);
                Assert.Empty(requestHeaders.GetValues("If-None-Match"));
                Assert.Equal("spacervalue", requestHeaders["Spacer-Header"]);
                context.Dispose();

                await responseTask;
            }
        }
#endif

        [ConditionalFact]
        public async Task RequestHeaders_ClientSendsUnknownHeaderWithNoValue_Success()
        {
            string address;
            using (var server = Utilities.CreateHttpServer(out address))
            {
                string[] customValues = new string[] { "" };
                Task responseTask = SendRequestAsync(address, "Custom-Header", customValues);

                var context = await server.AcceptAsync(Utilities.DefaultTimeout);
                var requestHeaders = context.Request.Headers;
                Assert.Equal(3, requestHeaders.Count);
                Assert.Equal(new Uri(address).Authority, requestHeaders["Host"]);
                Assert.Equal(new[] { new Uri(address).Authority }, requestHeaders.GetValues("Host"));
                Assert.Equal("", requestHeaders["Custom-Header"]);
                Assert.Empty(requestHeaders.GetValues("Custom-Header"));
                Assert.Equal("spacervalue", requestHeaders["Spacer-Header"]);
                context.Dispose();

                await responseTask;
            }
        }

        private async Task<string> SendRequestAsync(string uri)
        {
            using (HttpClient client = new HttpClient())
            {
                return await client.GetStringAsync(uri);
            }
        }

        private async Task SendRequestAsync(string address, string customHeader, string[] customValues)
        {
            var uri = new Uri(address);
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"GET {uri.AbsolutePath} HTTP/1.1");
            builder.AppendLine("Connection: close");
            builder.Append("HOST: ");
            builder.AppendLine(uri.Authority);
            foreach (string value in customValues)
            {
                builder.Append(customHeader);
                builder.Append(": ");
                builder.AppendLine(value);
                builder.AppendLine("Spacer-Header: spacervalue");
            }
            builder.AppendLine();

            byte[] request = Encoding.UTF8.GetBytes(builder.ToString());

            Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(uri.Host, uri.Port);
            using (SslStream sslStream = new SslStream(new NetworkStream(socket)))
            {
                sslStream.AuthenticateAsClient(uri.Host);
                sslStream.Write(request);

                byte[] response = new byte[1024 * 5];
                await Task.Run(() => sslStream.Read(response, 0, response.Length));
            }
            socket.Dispose();
        }
    }
}

#endif