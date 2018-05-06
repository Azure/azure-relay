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
    using System.IO;
    using System.Threading.Tasks;

    class Program : ITcpSenderSample
    {
        public async Task Run(string sendAddress, string sendToken)
        {
            using (var streamClient = new StreamClient(sendAddress, sendToken, isDynamicEndpoint : false))
            {
                var stdout = Console.Out;
                Console.WriteLine("Don't look here!");
                Console.SetOut(new StreamWriter(streamClient) { AutoFlush = true });
                Console.WriteLine("Look over here!");
                Console.WriteLine("The output is printed on the server!");
                for (int i = 0; i < 100; i++)
                {
                    Console.WriteLine(i);
                }
                Console.SetOut(stdout);

                Console.ReadLine();
            }
        }
    }
}