# Azure Hybrid Connections samples for .NET Standard

- [**Simple**](./simple/README.md) - The "Simple" sample illustrates the basic functions of the API
and shows how to bi-directionally exchange blocks of text over a connection
- [**Thrift**](./thrift/README.md) - The "Thrift" sample is a variation of the C# sample that is 
part of the Apache Thrift project and shows how to use the Thrift RPC model
over Hybrid Connections.
- [**Bond**](./bond/README.md) - The "Bond" sample illustrates how to use Microsoft Bond 
Comm RPC with the Relay. The sample includes an standardalone implementation of 
an alternate Epoxy transport that uses Hybrid Connections instead of TCP.
- [**PortBridge**](./portbridge/README.md) - The "PortBridge" sample is a port of one of the 
classic flagship samples of the WCF Relay capability over to the new Hybrid Connection 
Relay. PortBridge creates multiplexed socket tunnels that can bridge TCP and Named Pipe 
socket connections across the Relay, including SQL Server and Remote Deskop Connections.