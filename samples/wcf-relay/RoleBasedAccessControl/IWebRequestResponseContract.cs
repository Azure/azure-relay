//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace RoleBasedAccessControl
{
    using System.ServiceModel;
    using System.ServiceModel.Web;

    [ServiceContract]
    interface IWebRequestResponse
    {
        [OperationContract, WebGet(UriTemplate = "/")]
        string SimpleGetString();
        [OperationContract, WebInvoke(Method = "POST", UriTemplate = "/")]
        string SimplePostString(string text);
        [OperationContract, WebGet(UriTemplate = "/{text1}/{text2}")]
        string GetWithUriArgs(string text1, string text2);
    }

    [ServiceContract]
    interface IWebRequestResponseJson
    {
        [OperationContract, WebGet(UriTemplate = "?text={text}", ResponseFormat = WebMessageFormat.Json)]
        string GetJsonString(string text);
        [OperationContract, WebInvoke(Method = "POST", UriTemplate = "/", ResponseFormat = WebMessageFormat.Json)]
        string PostJsonString(string text);
        [OperationContract, WebInvoke(Method = "POST", UriTemplate = "/multi", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest)]
        string PostMultiJsonStrings(string text1, string text2);
    }

    [ServiceContract]
    interface IWebStatusCodeCheck
    {
        [OperationContract, WebGet(UriTemplate = "/ok")]
        string GetWithOkStatusCode();
        [OperationContract, WebGet(UriTemplate = "/notfound")]
        string GetWithNoFoundStatusCodeStringReturnValue();
        [OperationContract, WebGet(UriTemplate = "/notfoundnullvalue")]
        string GetWithNoFoundStatusCodeNullReturnValue();
    }

    [ServiceContract]
    interface IWebRequestResponseChannel : IWebRequestResponse, IClientChannel { }

    [ServiceContract]
    interface IWebRequestResponseJsonChannel : IWebRequestResponseJson, IClientChannel { }

    [ServiceContract]
    interface IWebStatusCodeCheckChannel : IWebStatusCodeCheck, IClientChannel { }
}
