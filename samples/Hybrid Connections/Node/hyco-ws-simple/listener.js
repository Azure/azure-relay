if (process.argv.length < 6) {
    console.log('listener.js [namespace] [path] [key-rule] [key]');
} else {

    var ns = process.argv[2];
    var path = process.argv[3];
    var keyrule = process.argv[4];
    var key = process.argv[5];

    var WebSocket = require('hyco-ws')

    var uri = WebSocket.createRelayListenUri(ns, path);
    var wss = WebSocket.createRelayedServer(
        {
            server : uri,
            token: function () {
                return WebSocket.createRelayToken(uri, keyrule, key);
            }
        },
        function(ws) {
            console.log('connection accepted');
            ws.onmessage = function(event) {
                console.log('onmessage: ' + event.data);
            };
            ws.on('close', function() {
                console.log('connection closed');
            });
        }
    );

    wss.on('error', function(err) {
        console.log('error: ' + err);
    });
}