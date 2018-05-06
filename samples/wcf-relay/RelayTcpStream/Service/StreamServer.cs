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
    using System.ServiceModel;
    using System.Threading.Tasks;

    [ServiceContract(Namespace = "", Name = "txf", SessionMode = SessionMode.Required)]
   class StreamServer
    {
        readonly Stream readStream;
        readonly Stream writeStream;


        public StreamServer(Stream stream):this(stream, stream)
        {
            
        }
        public StreamServer(Stream readStream, Stream writeStream)
        {
            this.readStream = readStream;
            this.writeStream = writeStream;
        }

        [OperationContract]
        Task WriteAsync(byte[] data)
        {
            return this.writeStream.WriteAsync(data, 0, data.Length);
        }

        [OperationContract]
        async Task<byte[]> ReadAsync(int max)
        {
            byte[] buffer = new byte[max];
            int bytesRead = await this.readStream.ReadAsync(buffer, 0, max);
            if (bytesRead < max)
            {
                var result = new byte[bytesRead];
                Array.ConstrainedCopy(buffer, 0, result, 0, bytesRead);
                return result;
            }
            return buffer;
        }
    }


  
}