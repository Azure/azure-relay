if (process.argv.length < 6) {
    console.log('sender.js [namespace] [path] [key-rule] [key]');
} else {

    var ns = process.argv[2];
    var path = process.argv[3];
    var keyrule = process.argv[4];
    var key = process.argv[5];

    var WebSocket = require('hyco-ws')

    var uri = WebSocket.createRelaySendUri(ns, path);
    WebSocket.relayedConnect(
        uri,
        WebSocket.createRelayToken(uri, keyrule, key),
        function(wss) {
            var id = setInterval(function() {
                wss.send(JSON.stringify(process.memoryUsage()), function() { /* ignore errors */ });
            }, 500);

            console.log('Started client interval. Press any key to stop.');
            wss.on('close', function() {
                console.log('stopping client interval');
                clearInterval(id);
                process.exit();
            });

            process.stdin.setRawMode(true);
            process.stdin.resume();
            process.stdin.on('data', function() {
                wss.close();
            });
        }
    );
}