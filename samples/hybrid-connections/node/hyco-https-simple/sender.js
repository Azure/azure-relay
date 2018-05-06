var https = require('hyco-https')

var args = { /* defaults */
    ns: process.env.SB_HC_NAMESPACE,
    path: process.env.SB_HC_PATH,
    keyrule: process.env.SB_HC_KEYRULE,
    key: process.env.SB_HC_KEY
};

/* Parse command line options */
var pattern = /^--(.*?)(?:=(.*))?$/;
process.argv.forEach(function (value) {
    var match = pattern.exec(value);
    if (match) {
        args[match[1]] = match[2] ? match[2] : true;
    }
});

if (args.ns == null || args.path == null || args.keyrule == null || args.key == null) {
    console.log('sender.js --ns=[namespace] --path=[path] --keyrule=[keyrule] --key=[key]');
} else {

    var ns = args.ns; 
    var path = args.path;
    var keyrule = args.keyrule;
    var key = args.key;

    https.get({
        hostname : ns,
        path : ((!path || path.length == 0 || path[0] !== '/')?'/':'') + path,
        port : 443,
        headers : {
            'ServiceBusAuthorization' : 
               https.createRelayToken(https.createRelayHttpsUri(ns, path), keyrule, key)
        }
    }, (res) => {
        let error;
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