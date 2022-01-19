const https = require('hyco-https')
const azureIdentity = require("@azure/identity");
const util = require("util");
const relay_aad_audience = "https://relay.azure.net//.default"; // this constant is the url of Azure Relay as a resource provider to authorize the token against.

var args = { /* defaults */
    ns: process.env.SB_HC_NAMESPACE,
    path: process.env.SB_HC_PATH
};

/* Parse command line options */
var pattern = /^--(.*?)(?:=(.*))?$/;
process.argv.forEach(function (value) {
    var match = pattern.exec(value);
    if (match) {
        args[match[1]] = match[2] ? match[2] : true;
    }
});

var getTokenFunction = null;
if (args.ns != null && args.path != null && args.tenantid != null && args.clientid != null && args.clientsecret != null) {
    getTokenFunction = getTokenWithClientSecret;
} else if (args.ns != null && args.path != null && args.clientid != null) {
    getTokenFunction = getTokenWithUserAssignedIdentity;
} else if (args.ns != null && args.path != null) {
    getTokenFunction = getTokenWithAzureManagedIdentity;
} else {
    console.log('To use AzureManagedIdentityTokenCredentials, please use the following in commnd line and input the corresponding prams: node listener.js --ns=[namespace] --path=[path]');
    console.log('To use UserAssignedManagedIdentityTokenCredentials, please use the following in commnd line and input the corresponding prams: node listener.js --ns=[namespace] --path=[path] --clientid=[clientid]');
    console.log('To use ClientSecretTokenCredentials, please use the following in commnd line and input the corresponding prams: node listener.js --ns=[namespace] --path=[path] --clientid=[clientid] --clientsecret=[clientsecret] --tenantid=[tenantid]');
    process.exit(1);
}

sendHttpsRequest(getTokenFunction);

async function getTokenWithClientSecret() {
    console.log("Try getting the token using Client Secret...");
    var tokenString;
    var clientSecretTokenCredential = new azureIdentity.ClientSecretCredential(args.tenantid, args.clientid, args.clientsecret);
    await clientSecretTokenCredential.getToken(relay_aad_audience).then(
        (accessToken) => {
            console.log("Got the token successfully.");
            tokenString = accessToken.token;
        },
        (reason) => {
            // get token failed
            console.error(reason);
        });
    
    return tokenString;
}

async function getTokenWithUserAssignedIdentity() {
    console.log("Try getting the token using User Assigned Managed Identity...");
    var tokenString;
    var mangedIdentityTokenCredential = new azureIdentity.ManagedIdentityCredential(args.clientid);
    await mangedIdentityTokenCredential.getToken(relay_aad_audience).then(
        (accessToken) => {
            console.log("Got the token successfully.");
            tokenString = accessToken.token;
        },
        (reason) => {
            // get token failed
            console.error(reason);
        });
    
    return tokenString;
}

async function getTokenWithAzureManagedIdentity() {
    console.log("Try getting the token using Azure Managed Identity...");
    var tokenString;
    var mangedIdentityTokenCredential = new azureIdentity.ManagedIdentityCredential();
    await mangedIdentityTokenCredential.getToken(relay_aad_audience).then(
    (accessToken) => {
        console.log("Got the token successfully.");
        tokenString = accessToken.token;
    },
    (reason) => {
        // get token failed
        console.error(reason);
    });

    return tokenString;
}

async function sendHttpsRequest(getTokenFunction) {
    console.log(getTokenFunction);
    https.get({
        hostname : args.ns,
        path : ((!args.path || args.path.length == 0 || args.path[0] !== '/')?'/':'') + args.path,
        port : 443,
        headers : {
            'ServiceBusAuthorization' : await getTokenFunction()
        }
    }, (res) => {
        if (res.statusCode !== 200) {
            console.error('Request Failed.\n Status Code:' + res.statusCode);
            res.resume();
        } 
        else {
            res.setEncoding('utf8');
            res.on('data', (chunk) => {
                console.log(`BODY: ${chunk}`);
            });
            res.on('end', () => {
                console.log('No more data in response.');
            });
        };
    }).on('error', (e) => {
        console.error(`Got error: ${e.message}`);
    });
}