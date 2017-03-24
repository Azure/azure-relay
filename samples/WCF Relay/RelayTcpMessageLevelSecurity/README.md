<P>This sample demonstrates using the <B>NetTcpRelayBinding</B> binding with 
message security.</P>
<H2 class=heading>Prerequisites</H2>
<DIV id=sectionSection0 class=section><content 
xmlns="http://ddue.schemas.microsoft.com/authoring/2003/5">
<P xmlns="">If you haven't already done so, read the release notes document that 
explains how to sign up for a Windows Azure account and how to 
configure your environment. 
</P>
<P xmlns="">Before running the sample, you must run the Setup.bat script from 
the solution directory in a Visual Studio 2010 (or above) or a Windows SDK command prompt 
running with administrator privileges. The setup script creates and installs an 
X.509 test certificate that is used as the service identity. After running the 
sample, you should run the Cleanup.bat script to remove the certificate. </P>
				</content></DIV>
<H2 class=heading>Echo Service</H2>
<DIV id=sectionSection1 class=section><content 
xmlns="http://ddue.schemas.microsoft.com/authoring/2003/5">
<P xmlns="">The service implements a simple contract with a single 
operation named Echo. The Echo service accepts a string and echoes the string 
back.</P>
<DIV class=code xmlns=""><SPAN codeLanguage="CSharp">
<TABLE cellSpacing=0 cellPadding=0 width="100%">
  <TBODY>
  <TR>
    <TH>C#&nbsp;</TH>
</TR>
  <TR>
    <TD colSpan=2><PRE>[ServiceBehavior(Name = "EchoService", Namespace = "http://samples.microsoft.com/ServiceModel/Relay/")]
class EchoService : IEchoContract
{
    public string Echo(string text)
    {
        Console.WriteLine("Echoing: {0}", text);
        return text;            
    }
}</PRE></TD></TR></TBODY></TABLE></SPAN></DIV>
<P xmlns="">The service configuration contains one active and two additional 
optional service settings. The default endpoint configuration refers to a 
<B>NetTcpRelayBinding</B> binding configuration that uses a 
<CODE>UserName</CODE> credential for message security. The alternate 
<CODE>relayClientAuthenticationNone</CODE> configuration refers to a 
<B>NetTcpRelayBinding</B> binding configuration that uses a 
<CODE>UserName</CODE> credential for message security, and also disables the 
relay client authentication. The second alternate configuration, 
<CODE>transportWithMessageCredential</CODE>, uses a message credential for 
end-to-end authentication/authorization, but relies on SSL for message 
protection. </P>
<P xmlns="">To secure the endpoint, the service is configured with the 
<CODE>usernamePasswordServiceBehavior</CODE> behavior. This behavior contains 
the service credentials (backed by the test certificate generated and installed 
by the Setup.bat script) and refers to the 
<CODE>SimpleUserNamePasswordValidator</CODE> in the service project that 
authenticates the credentials. This validator recognizes two hard-coded username&nbsp; and password combinations: test1/1tset and test2/2tset. Refer to the 
WCF Authentication documentation section for information about implementing other 
credential validators.</P>
<DIV class=code xmlns=""><SPAN codeLanguage="xml">
<TABLE cellSpacing=0 cellPadding=0 width="100%">
  <TBODY>
  <TR>
    <TH>Xml&nbsp;</TH>
</TR>
  <TR>
    <TD colSpan=2><PRE>&lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot; ?&gt;
&lt;configuration&gt;
   &lt;system.serviceModel&gt;
      &lt;behaviors&gt;
         &lt;serviceBehaviors&gt;
            &lt;behavior name=&quot;usernamePasswordServiceBehavior&quot;&gt;
               &lt;serviceCredentials&gt;
                  &lt;serviceCertificate findValue=&quot;localhost&quot; storeLocation=&quot;LocalMachine&quot; storeName=&quot;My&quot; x509FindType=&quot;FindBySubjectName&quot; /&gt;
                  &lt;userNameAuthentication userNamePasswordValidationMode=&quot;Custom&quot; 
                         includeWindowsGroups=&quot;false&quot; customUserNamePasswordValidatorType=&quot;Microsoft.ServiceBus.Samples.SimpleUsernamePasswordValidator, 
                         NetTcpRelayMsgSecUserNameService&quot; /&gt;
               &lt;/serviceCredentials&gt;
            &lt;/behavior&gt;
         &lt;/serviceBehaviors&gt;
         &lt;endpointBehaviors&gt;
            &lt;behavior name=&quot;sharedSecretEndpointBehavior&quot;&gt;
               &lt;transportClientEndpointBehavior credentialType=&quot;SharedSecret&quot;&gt;
                  &lt;clientCredentials&gt;
                     &lt;sharedSecret issuerName=&quot;ISSUER_NAME&quot; issuerSecret=&quot;ISSUER_SECRET&quot; /&gt;
                  &lt;/clientCredentials&gt;
               &lt;/transportClientEndpointBehavior&gt; 
            &lt;/behavior&gt;
         &lt;/endpointBehaviors&gt;
      &lt;/behaviors&gt;
      &lt;bindings&gt;
         &lt;netTcpRelayBinding&gt;
            &lt;!-- Default Binding Configuration--&gt;
            &lt;binding name=&quot;default&quot;&gt;
               &lt;security mode=&quot;Message&quot;&gt;
                  &lt;message clientCredentialType=&quot;UserName&quot;/&gt;
               &lt;/security&gt;
            &lt;/binding&gt;
            &lt;!-- Alternate Binding Configuration #1: Disabling Client Relay Authentication --&gt;
            &lt;binding name=&quot;relayClientAuthenticationNone&quot;&gt;
               &lt;security mode=&quot;Message&quot; relayClientAuthenticationType=&quot;None&quot;&gt;
                  &lt;message clientCredentialType=&quot;UserName&quot;/&gt;
               &lt;/security&gt;
            &lt;/binding&gt;
            &lt;!-- Alternate Binding Configuration #2: Transport With Message Credential --&gt;
            &lt;binding name=&quot;transportWithMessageCredential&quot;&gt;
               &lt;security mode=&quot;TransportWithMessageCredential&quot;&gt;
                  &lt;message clientCredentialType=&quot;UserName&quot;/&gt;
               &lt;/security&gt;
            &lt;/binding&gt;
         &lt;/netTcpRelayBinding&gt;
      &lt;/bindings&gt;

      &lt;services&gt;
      &lt;!-- Application Service --&gt;
         &lt;service name=&quot;Microsoft.ServiceBus.Samples.EchoService&quot; behaviorConfiguration=&quot;usernamePasswordServiceBehavior&quot;&gt;
            &lt;!-- 
               Default configuration. You must comment out the following declaration whenever you want to use any of the alternate configurations below. 
            --&gt;
            &lt;endpoint name=&quot;RelayEndpoint&quot;
               contract=&quot;Microsoft.ServiceBus.Samples.IEchoContract&quot;
               binding=&quot;netTcpRelayBinding&quot;
               bindingConfiguration=&quot;default&quot; 
               behaviorConfiguration=&quot;sharedSecretEndpointBehavior&quot; 
               address=&quot;&quot; /&gt;

             &lt;!-- Alternatively use the endpoint configuration below to enable alternate configuration #1 --&gt;
             &lt;!--
             &lt;endpoint name=&quot;RelayEndpoint&quot;
               contract=&quot;Microsoft.ServiceBus.Samples.IEchoContract&quot;
               binding=&quot;netTcpRelayBinding&quot;
               bindingConfiguration=&quot;relayClientAuthenticationNone&quot;
               behaviorConfiguration=&quot;sharedSecretEndpointBehavior&quot;
               address=&quot;&quot; /&gt;
             --&gt;

            &lt;!-- Alternatively use the endpoint configuration below to enable alternate configuration #2 --&gt;
            &lt;!--
            &lt;endpoint name=&quot;RelayEndpoint&quot;
              contract=&quot;Microsoft.ServiceBus.Samples.IEchoContract&quot;
              binding=&quot;netTcpRelayBinding&quot;
              bindingConfiguration=&quot;transportWithMessageCredential&quot;
              behaviorConfiguration=&quot;sharedSecretEndpointBehavior&quot;
              address=&quot;&quot; /&gt;
            --&gt;
         &lt;/service&gt;
      &lt;/services&gt;
   &lt;/system.serviceModel&gt;
&lt;/configuration&gt;
</PRE>
	</TD></TR></TBODY></TABLE></SPAN></DIV>
<H2 class=heading>Echo Client</H2>
				<P xmlns="">The client is similar to the Echo sample client, but differs in configuration and how the channel factory is 
configured with the correct end-to-end credentials.</P>
<DIV class=code xmlns=""><SPAN codeLanguage="CSharp">
<TABLE cellSpacing=0 cellPadding=0 width="100%">
  <TBODY>
  <TR>
    <TH>C#&nbsp;</TH>
</TR>
  <TR>
    <TD colSpan=2><PRE>ChannelFactory&lt;IEchoChannel&gt; channelFactory = new ChannelFactory&lt;IEchoChannel&gt;("RelayEndpoint", new EndpointAddress(serviceUri, 
    EndpointIdentity.CreateDnsIdentity("localhost")));
channelFactory.Credentials.UserName.UserName = "test1";
channelFactory.Credentials.UserName.Password = "1tset";</PRE></TD></TR></TBODY></TABLE></SPAN></DIV>
<P xmlns="">Note that the <CODE>ChannelFactory</CODE> is constructed using an 
<CODE>EndpointAddress</CODE> that has an explicit DNS 
<CODE>EndpointIdentity</CODE>. However, the identity is not directly related to DNS but rather to the certificate 
subject name. The identity name (<CODE>localhost</CODE> in this case) refers 
directly to the subject name of the certificate that is specified for the 
service identity in the <CODE>usernamePasswordServiceBehavior</CODE> behavior in 
the service. For an actual implementation, the service identity should be 
backed by a production certificate issued by a trusted certificate authority 
(CA) and the <CODE>EndpointIdentity</CODE> must refer to its subject name. 
</P>
<P xmlns="">The client configuration mirrors the service configuration, with a 
few exceptions. The client endpoints are configured with the 
<CODE>usernamePasswordEndpointBehavior</CODE> behavior with 
<CODE>&lt;clientCredentials&gt;</CODE> settings that disable certificate 
validation specifically for the test certificate being used. For an actual 
implementation that uses a CA-issued certificate, you should omit this 
override.</P>
<DIV class=code xmlns=""><SPAN codeLanguage="xml">
<TABLE cellSpacing=0 cellPadding=0 width="100%">
  <TBODY>
  <TR>
    <TH>Xml&nbsp;</TH>
</TR>
  <TR>
    <TD colSpan=2><PRE class="style1">&lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot; ?&gt;
&lt;configuration&gt;
   &lt;system.serviceModel&gt;
      &lt;behaviors&gt;
         &lt;endpointBehaviors&gt;
            &lt;behavior name=&quot;sharedSecretEndpointBehavior&quot;&gt;
               &lt;transportClientEndpointBehavior credentialType=&quot;SharedSecret&quot;&gt;
                  &lt;clientCredentials&gt;
                     &lt;sharedSecret issuerName=&quot;ISSUER_NAME&quot; issuerSecret=&quot;ISSUER_SECRET&quot; /&gt;
                  &lt;/clientCredentials&gt;
               &lt;/transportClientEndpointBehavior&gt;
               &lt;clientCredentials&gt;
                  &lt;serviceCertificate&gt;
                     &lt;authentication certificateValidationMode=&quot;None&quot; /&gt;
                  &lt;/serviceCertificate&gt;
               &lt;/clientCredentials&gt;
            &lt;/behavior&gt;
            &lt;behavior name=&quot;noCertificateValidationEndpointBehavior&quot;&gt; 
               &lt;clientCredentials&gt;
                  &lt;serviceCertificate&gt;
                     &lt;authentication certificateValidationMode=&quot;None&quot; /&gt;
                  &lt;/serviceCertificate&gt;
               &lt;/clientCredentials&gt;
            &lt;/behavior&gt;
         &lt;/endpointBehaviors&gt;
      &lt;/behaviors&gt;
      &lt;bindings&gt;
      &lt;!-- Application Binding --&gt;
      &lt;netTcpRelayBinding&gt;
         &lt;!-- Default Binding Configuration--&gt;
         &lt;binding name=&quot;default&quot;&gt;
            &lt;security mode=&quot;Message&quot;&gt;
               &lt;message clientCredentialType=&quot;UserName&quot;/&gt;
            &lt;/security&gt;
         &lt;/binding&gt;
         &lt;!-- Alternate Binding Configuration #1: Disabling Client Relay Authentication --&gt;
         &lt;binding name=&quot;relayClientAuthenticationNone&quot;&gt;
            &lt;security mode=&quot;Message&quot; relayClientAuthenticationType=&quot;None&quot;&gt;
               &lt;message clientCredentialType=&quot;UserName&quot;/&gt;
            &lt;/security&gt;
         &lt;/binding&gt;
         &lt;!-- Alternate Binding Configuration #2: Transport With Message Credential --&gt;
         &lt;binding name=&quot;transportWithMessageCredential&quot;&gt;
            &lt;security mode=&quot;TransportWithMessageCredential&quot;&gt;
               &lt;message clientCredentialType=&quot;UserName&quot;/&gt;
            &lt;/security&gt;
         &lt;/binding&gt;
      &lt;/netTcpRelayBinding&gt;
   &lt;/bindings&gt;
</PRE>
	<PRE class="style1">     &lt;client&gt;
         &lt;!-- Default configuration. You must comment out the following declaration whenever 
            you want to use any of the alternate configurations below. 
        --&gt;
         &lt;endpoint name=&quot;RelayEndpoint&quot;
            contract=&quot;Microsoft.ServiceBus.Samples.IEchoContract&quot;
            binding=&quot;netTcpRelayBinding&quot;
            bindingConfiguration=&quot;default&quot;
            behaviorConfiguration=&quot;sharedSecretEndpointBehavior&quot;
            address=&quot;&quot; /&gt;

         &lt;!-- Alternatively use the endpoint configuration below to enable alternate configuration #1 --&gt;
         &lt;!--
         &lt;endpoint name=&quot;RelayEndpoint&quot;
            contract=&quot;Microsoft.ServiceBus.Samples.IEchoContract&quot;
            binding=&quot;netTcpRelayBinding&quot;
            bindingConfiguration=&quot;relayClientAuthenticationNone&quot; 
            behaviorConfiguration=&quot;noCertificateValidationEndpointBehavior&quot;
            address=&quot;&quot; /&gt;
         --&gt; 

         &lt;!-- Alternatively use the endpoint configuration below to enable alternate configuration #2 --&gt;
         &lt;!--
         &lt;endpoint name=&quot;RelayEndpoint&quot;
            contract=&quot;Microsoft.ServiceBus.Samples.IEchoContract&quot;
            binding=&quot;netTcpRelayBinding&quot;
            bindingConfiguration=&quot;transportWithMessageCredential&quot;
            behaviorConfiguration=&quot;sharedSecretEndpointBehavior&quot;
            address=&quot;&quot; /&gt;
         --&gt;
      &lt;/client&gt;
</PRE>
	<PRE class="style1">   &lt;/system.serviceModel&gt;
&lt;/configuration&gt;</PRE>
	</TD></TR></TBODY></TABLE></SPAN></DIV></content></DIV>
<H2 class=heading>Running the Sample</H2>
<DIV id=sectionSection2 class=section><content 
xmlns="http://ddue.schemas.microsoft.com/authoring/2003/5">
<P xmlns="">Substitute the ISSUER_NAME and ISSUER_SECRET strings in the 
service and client App.config files with appropriate values.</P>
<content 
xmlns="http://ddue.schemas.microsoft.com/authoring/2003/5">
    <p xmlns="">
        To generate and install the self-issued cerificate used by the sample, run the 
        setup.bat file included in the sample solution from a Visual Studio command line 
        window running with Administrator privileges.</p>
				<P xmlns="">To run the sample, build the solution in Visual 
				Studio or from the command line, then run the two resulting 
				executables from a command prompt. 
				Start the service first, then start the client application.</P>
<P xmlns="">When the service and the client are running, you can start typing 
messages into the client application. These messages are echoed by the 
service.</P>
<content 
xmlns="http://ddue.schemas.microsoft.com/authoring/2003/5">
    <p xmlns="">
        After stopping the client and service you can run cleanup.bat from a Visual 
        Studio command line window with Administrator privileges to remove the sample 
        certificate from your computer&#39;s local store.</p>
<P xmlns=""><B>Expected Output – Client</B></P>
<DIV class=code xmlns=""><SPAN codeLanguage="other">
<TABLE cellSpacing=0 cellPadding=0 width="100%">
  <TBODY>
  <TR>
    <TD colSpan=2><PRE>Enter the Service Namespace you want to connect to: &lt;Service Namespace&gt;
Enter text to echo (or [Enter] to exit): Hello, World!
Server echoed: Hello, World!</PRE></TD></TR></TBODY></TABLE></SPAN></DIV>
<P xmlns=""><B>Expected Output – Service</B></P>
<DIV class=code xmlns=""><SPAN codeLanguage="other">
<TABLE cellSpacing=0 cellPadding=0 width="100%">
  <TBODY>
  <TR>
    <TD colSpan=2><PRE><SPAN codeLanguage="other">Enter the Service Namespace you want to connect to: &lt;Service Namespace&gt;
</SPAN>Service address: sb://&lt;serviceNamespace&gt;.servicebus.windows.net/EchoService/
Press [Enter] to exit
Echoing: Hello, World!</PRE></TD></TR></TBODY></TABLE></SPAN></DIV></content></DIV><!--[if gte IE 5]><tool:tip 
avoidmouse="false" element="languageFilterToolTip"></tool:tip><![endif]--></DIV>
<P>
<P xmlns="">
    <hr /> 
    Did you find this information useful?
    <a href="http://go.microsoft.com/fwlink/?LinkID=155664">
        Please send your suggestions and comments about the documentation.

    </a></P></DIV></BODY></HTML>
