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

namespace Client
{
    using System;
    using System.Drawing;
    using System.Net.Http;
    using System.Windows.Forms;

    public partial class ClientForm : Form
    {
        readonly string sendAddress;
        readonly string sendToken;

        public ClientForm()
        {
            this.InitializeComponent();
        }

        public ClientForm(string sendAddress, string sendToken)
        {
            this.InitializeComponent();
            this.sendAddress = sendAddress;
            this.sendToken = sendToken;
        }

        protected override void OnLoad(EventArgs e)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("ServiceBusAuthorization", this.sendToken);
            var image = client.GetStreamAsync(this.sendAddress + "/image").GetAwaiter().GetResult();
            this.pictureBox.Image = Image.FromStream(image);
        }
    }
}