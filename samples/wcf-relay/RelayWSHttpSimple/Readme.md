
 
Simple Sample 
 




This sample demonstrates using the WS2007HttpRelayBinding binding. It demonstrates a simple service that uses no security options and does not require clients to authenticate. 

Prerequisites


If you haven't already done so, read the release notes document that explains how to sign up for a Windows Azure account and how to configure your environment. 
  
Service


The service project defines a simple contract, IEchoContract, with a single operation named  Echo. The Echo servic service accepts a string and echoes it back.  



C#  



[ServiceBehavior(Name = "EchoService", Namespace = "http://samples.microsoft.com/ServiceModel/Relay/")]
class EchoService : IEchoContract
{
    public string Echo(string text)
    {
        Console.WriteLine("Echoing: {0}", text);
        return text;            
    }
}
 

 

The endpoints for this service are defined in the application configuration file. Specifically, the following endpoint is defined: 



Xml 

<system.serviceModel>
    
    <bindings>
      <!-- Binding configuration-->
      <ws2007HttpRelayBinding>
        <binding name="NoSecNoAuth">
          <!-- No message or transport security. Allow unauthenticated clients. -->
          <security mode="None" relayClientAuthenticationType="None"/>
        </binding>
      </ws2007HttpRelayBinding>
    </bindings>

    <services>
      <!-- Service configuration-->
      <service name="Microsoft.ServiceBus.Samples.EchoService">
        <endpoint name="ServiceBusEndpoint"
                  contract="Microsoft.ServiceBus.Samples.IEchoContract"
                  binding="ws2007HttpRelayBinding"
                  bindingConfiguration="NoSecNoAuth" />        
      </service>
    </services>

</system.serviceModel>
 
 
Client


The client is configured (also in the application configuration file) with the following endpoint: 
 


Xml 

<system.serviceModel>
    
    <bindings>
      <!-- Binding configuration-->
      <ws2007HttpRelayBinding>
        <binding name="NoSecNoAuth">
          <!-- No message or transport security. Allow unauthenticated clients. -->
          <security mode="None" relayClientAuthenticationType="None"/>
        </binding>
      </ws2007HttpRelayBinding>
    </bindings>

    <client>
      <!-- Client endpoint configuration -->
      <endpoint name="ServiceBusEndpoint"
                contract="Microsoft.ServiceBus.Samples.IEchoContract"
                binding="ws2007HttpRelayBinding"
                bindingConfiguration="NoSecNoAuth" />
    </client>

</system.serviceModel>
 
  
Running the Sample


To run the sample, build the solution in Visual Studio or from the command line, then run the two resulting executable files. Start the service first using a command prompt with administrator privileges, then run the client. You would be prompted to enter the Issuer Name and Issuer Secret of the namespace you are using to run this sample. 

When the service and the client are running, you can start typing messages into the client application. These messages are echoed by the service. 

Expected Output – Client 


Your Service Namespace: <service namespace>
Enter text to echo (or [Enter] to exit): 
Hello, World!
Server echoed: Hello, World!
 

Expected Output – Service 


Your Service Namespace: <service namespace>
Your Issuer Name: <issuer name>
Your Issuer Secret: <issuer secret>
Service address: http://<service namespace>.servicebus.windows.net/HttpEchoService/
Press [Enter] to exit: Hello, World!
 
  


Did you find this information useful?  Please send your suggestions and comments about the documentation.  
