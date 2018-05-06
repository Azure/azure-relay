# Simple Hybrid Connections Sample  

This sample illustrates how to use the 'hyco-websocket' derivative of the 'websocket' Node 
package. Unlike the sample inside the package code, this variant builds on the 
published NPM package.  

## Usage

On a server, run `listener.js` specifying the namespace and path for a previously created 
Azure Relay Hybrid Connection, as well as a SAS rule name and key that grants "Listen" permission 
for that path:

`node listener.js myns.servicebus.windows.net mypath listenrule [base64 key]`

On a client, run `sender.js` specifying namespace and path of an Azure Relay Hybid Connection with
an active listener, along with a SAS rule name and a key that grants "Send" permission:

`node sender.js myns.servicebus.windows.net mypath sendrule [base64 key]`

The sender client will connect through the Relay to the listener and send its current 
process memory usage recurringly and until stopped by pressing any key. The listener
will print the received information to the console.

## Load Balancing and Failover

You can use this sample to explore the "load balancing" and recovery capabilities of the Relay.

If you start multiple concurrent listeners on the same name, you will see that subsequent 
runs of the sender will be distributed across all connected listeners. You can have up to 
25 listeners concurrently listening on one name. If a listener is dropped (close it), you'll 
find the client that had an open connection to that listener promptly failing and a 
reconnect will be directed to one of the remaining listeners. 