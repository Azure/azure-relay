if (process.argv.length < 6) {
    console.log('sender.js [namespace] [path] [key-rule] [key]');
} else {

    var ns = process.argv[2];
    var path = process.argv[3];
    var keyrule = process.argv[4];
    var key = process.argv[5];

    var WebSocket = require('hyco-websocket')
    var WebSocketClient = WebSocket.client

    var address =  WebSocket.createRelaySendUri(ns, path);
    var token = WebSocket.createRelayToken(address, keyrule, key);

    var client = new WebSocketClient();
    client.connect(address, null, null, { 'ServiceBusAuthorization' : token});

    client.on('connect', function(connection) {
        var id = setInterval(function() {
            connection.send(JSON.stringify(process.memoryUsage()), function() { /* ignore errors */ });
        }, 500);

        console.log('Started client interval. Press any key to stop.');
        connection.on('close', function() {
            console.log('stopping client interval');
            clearInterval(id);
            process.exit();
        });

        process.stdin.setRawMode(true);
        process.stdin.resume();
        process.stdin.on('data', function() {
            connection.close();
        });
    });
}