# Simple Hybrid Connections HTTP Sample

This sample for Azure Relay Hybrid Connections uses the
['hyco-https'](https://www.npmjs.com/package/hyco-https) module that is built
on and extends the core ['https'](https://nodejs.org/api/https.html) Node
module. This module re-exports all exports of that base module and adds new
exports that enable integration with the Azure Relay service's Hybrid
Connections HTTP request feature.

Existing applications that `require('https')` can use this package instead with
`require('hyco-https')`. This allows an application residing anywhere to accept
HTTPS requests via a public endpoint.

## Code

 If you are familiar with the regular 'https' module, you will find the code
 below as familiar. Request and response and error handling is identical.

``` js

    const https = require('hyco-https');

    var args = { 
        ns : process.env.SB_HC_NAMESPACE, // fully qualified relay namespace
        path : process.env.SB_HC_PATH, // path of the Hybrid Connection
        keyrule : process.env.SB_HC_KEYRULE, // name of a SAS rule
        key : process.env.SB_HC_KEY // key of the SAS rule
    };
    
    var uri = https.createRelayListenUri(args.ns, args.path);
    var server = https.createRelayedServer(
        {
            server : uri,
            token : () => https.createRelayToken(uri, args.keyrule, args.key)
        },
        (req, res) => {
            console.log('request accepted: ' + req.method + ' on ' + req.url);
            res.setHeader('Content-Type', 'text/html');
            res.end('<html><head><title>Hey!</title></head><body>Relayed Node.js Server!</body></html>');
        });

    server.listen( (err) => {
            if (err) {
              return console.log('something bad happened', err)
            }          
            console.log(`server is listening`)
          });

    server.on('error', (err) => {
        console.log('error: ' + err);
    });
```

The `options` element supports a different set of arguments than the
`createServer()` since it is neither a standalone listener nor embeddable into
an existing HTTP listener framework. There are also fewer options available
since the listener management is largely delegated to the Relay service.

Constructor arguments:

- **server** (required) - the fully qualified URI for a Hybrid Connection name on which to listen, constructed with the https.createRelayListenUri() helper.
- **token** (required) - this argument *either* holds a previously issued token string *or* a callback
                         function that can be called to obtain such a token string. The callback option
                         is preferred as it allows token renewal.

## Usage

On a server, run `listener.js` specifying the namespace and path for a previously created 
Azure Relay Hybrid Connection, as well as a SAS rule name and key that grants "Listen" permission 
for that path:

`node listener.js --ns=myns.servicebus.windows.net --path=mypath --keyrule=listenrule --key=[base64 key]`

On a client, run `sender.js` specifying namespace and path of an Azure Relay Hybrid Connection with
an active listener, along with a SAS rule name and a key that grants "Send" permission:

`node sender.js --ns=myns.servicebus.windows.net --path=mypath --keyrule=sendrule --key=[base64 key]`

The sender client will connect through the Relay to the listener and send its current 
process memory usage recurringly and until stopped by pressing any key. The listener
will print the received information to the console.

## Load Balancing and Failover

You can use this sample to explore the "load balancing" and recovery capabilities of the Relay.

If you start multiple concurrent listeners on the same name, you will see that subsequent 
runs of the sender will be distributed across all connected listeners. You can have up to 
25 listeners concurrently listening on one name. If a listener is dropped (close it), you'll 
find the client that had an open connection to that listener promptly failing and any 
reconnect will be directed to one of the remaining listeners. 