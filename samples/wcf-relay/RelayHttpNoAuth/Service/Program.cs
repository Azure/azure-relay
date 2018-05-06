//   
//   Copyright © Microsoft Corporation, All Rights Reserved
// 
//   Licensed under the Apache License, Version 2.0 (the "License"); 
//   you may not use this file except in compliance with the License. 
//   You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0 
// 
//   THIS CODE IS PROVIDED *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
//   OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION
//   ANY IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A
//   PARTICULAR PURPOSE, MERCHANTABILITY OR NON-INFRINGEMENT.
// 
//   See the Apache License, Version 2.0 for the specific language
//   governing permissions and limitations under the License. 

namespace RelaySamples
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.ServiceModel;
    using System.ServiceModel.Web;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus;

    [ServiceContract]
    class Program : IDynamicListenerSample
    {
        static readonly Image SampleImage = System.Drawing.Image.FromFile("image.jpg");

        public async Task Run(string hostName, string listenToken)
        {
            string httpAddress = new UriBuilder("http", hostName, -1, "svc").ToString();
            using (var host = new WebServiceHost(GetType()))
            {
                host.AddServiceEndpoint(
                    GetType(),
                    new WebHttpRelayBinding(
                        EndToEndWebHttpSecurityMode.None,
                        RelayClientAuthenticationType.None) {IsDynamic = true},
                    httpAddress)
                    .EndpointBehaviors.Add(
                        new TransportClientEndpointBehavior(
                            TokenProvider.CreateSharedAccessSignatureTokenProvider(listenToken)));

                host.Open();

                Console.WriteLine("Starting a browser to see the image: ");
                Console.WriteLine(httpAddress + "/Image");
                Console.WriteLine();
                // launching the browser
                System.Diagnostics.Process.Start(httpAddress + "/Image");
                Console.WriteLine("Press [Enter] to exit");
                Console.ReadLine();

                host.Close();
            }
        }

        [OperationContract, WebGet]
        Stream Image()
        {
            var stream = new MemoryStream();
            SampleImage.Save(stream, ImageFormat.Jpeg);
            stream.Position = 0;
            WebOperationContext.Current.OutgoingResponse.ContentType = "image/jpeg";
            return stream;
        }
    }
}