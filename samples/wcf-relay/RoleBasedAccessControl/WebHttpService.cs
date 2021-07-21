//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.ServiceModel;

namespace RoleBasedAccessControl
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    class WebHttpService : IWebRequestResponse
    {
        internal WebHttpService()
        {
        }

        public string SimpleGetString()
        {
            return "Hello";
        }

        public string SimplePostString(string text)
        {
            return text;
        }

        public string GetWithUriArgs(string text1, string text2)
        {
            return text1 + text2;
        }
    }
}
