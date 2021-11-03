# Azure Hybrid Connections Examples

## HTTP Examples

The following samples are available for the ['hyco-https'](https://www.npmjs.com/package/hyco-https)
module that allows Node.js apps to listen for HTTP requests on the Relay. 

* [Simple Sender and Listener](./hyco-https-simple)

## Websocket Examples

These samples show how to listen for Websocket connections through the Relay.

### For 'websocket' users

If you're familiar with the ['websocket'](https://www.npmjs.com/package/websocket) Node package,
these examples are the place to start: 

* [Simple Sender and Listener](./hyco-websocket-simple)
* [End-to-end TCP tunneling](./hyco-websocket-tunnel)

### For 'ws' users

If you're familiar with the ['ws'](https://www.npmjs.com/package/ws) package, look at these
examples first:

* [Simple Sender and Listener](./hyco-ws-simple)
* [Web-Site embedded client](./hyco-ws-serverstats)

## Role Based Access Control Examples

To learn how to use the RBAC (Role Based Access Control) mechanism as an option to authenticate,
please look at these samples, based on which module you are using:

* [RBAC Authentication For HYCO-HTTPS](./rolebasedaccesscontrol/hyco-https)
* [RBAC Authentication For HYCO-WEBSOCKET](./rolebasedaccesscontrol/hyco-websocket)
* [RBAC Authentication For HYCO-WS](./rolebasedaccesscontrol/hyco-ws)