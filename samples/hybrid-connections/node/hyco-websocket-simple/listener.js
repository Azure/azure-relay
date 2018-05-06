
if (process.argv.length < 6) {
    console.log('listener.js [namespace] [path] [key-rule] [key]');
} else {

    var ns = process.argv[2];
    var path = process.argv[3];
    var keyrule = process.argv[4];
    var key = process.argv[5];

    var WebSocket = require('hyco-websocket');
    var WebSocketServer = require('hyco-websocket').relayedServer;

    var wss = new WebSocketServer(
        {
            server : WebSocket.createRelayListenUri(ns, path),
            token: function() {
                return WebSocket.createRelayToken('http://' + ns, keyrule, key);
            },
            autoAcceptConnections : true
        });
    wss.on('connect',
        function(ws) {
            console.log('connection accepted');
            ws.on('message', function(message) {
                if (message.type === 'utf8') {
                    try {
                        console.log(JSON.parse(message.utf8Data));
                    }
                    catch (e) {
                        // do nothing if there's an error.
                    }
                }
            });
            ws.on('close', function() {
                console.log('connection closed');
            });
        });

    wss.on('error', function(err) {
      console.log('error: ' + err);
    });
}