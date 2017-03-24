<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.0 Transitional//EN">
<HTML dir=ltr XMLNS:MSHelp = "http://msdn.microsoft.com/mshelp" xmlns:ddue = 
"http://ddue.schemas.microsoft.com/authoring/2003/5" xmlns:xlink = 
"http://www.w3.org/1999/xlink" xmlns:tool = "http://www.microsoft.com/tooltip" 
XMLNS:[default] http://ddue.schemas.microsoft.com/authoring/2003/5 = 
"http://ddue.schemas.microsoft.com/authoring/2003/5"><HEAD><TITLE>MetadataExchange Sample</TITLE>
<META content="text/html; CHARSET=utf-8" http-equiv=Content-Type></META>
<META name=save content=history></META><LINK rel=stylesheet type=text/css 
href="../../../CommonFiles/Classic.css"></LINK>

<META name=GENERATOR content="MSHTML 8.00.6001.18783">
<style type="text/css">
.style1 {
				font-family: monospace;
				font-size: 100%;
				color: #000000;
}
</style>
</HEAD>
<BODY><INPUT id=userDataCache class=userDataStyle type=hidden></INPUT><INPUT 
id=hiddenScrollOffset type=hidden></INPUT><IMG 
style="WIDTH: 0px; DISPLAY: none; HEIGHT: 0px" id=dropDownImage 
src="../../../../../Common/Html/drpdown.gif"></IMG><IMG 
style="WIDTH: 0px; DISPLAY: none; HEIGHT: 0px" id=dropDownHoverImage 
src="../../../../../Common/Html/drpdown_orange.gif"></IMG><IMG 
style="WIDTH: 0px; DISPLAY: none; HEIGHT: 0px" id=copyImage 
src="../../../../../Common/Html/copycode.gif"></IMG><IMG 
style="WIDTH: 0px; DISPLAY: none; HEIGHT: 0px" id=copyHoverImage 
src="../../../../../Common/Html/copycodeHighlight.gif"></IMG>
<DIV id=header>
<TABLE id=topTable width="100%">
  <TBODY>
  <TR id=headerTableRow1>
    <TD align=left><SPAN id=runningHeaderText></SPAN></TD></TR>
  <TR id=headerTableRow2>
    <TD align=left><SPAN id=nsrTitle>MetadataExchange Sample</SPAN></TD></TR>
  <TR id=headerTableRow3>
    <TD></TD></TR></TBODY></TABLE>
</DIV>
<DIV id=mainSection>
<DIV id=mainBody>
<DIV id=allHistory class=saveHistory onload="loadAll()" 
onsave="saveAll()"></DIV>
<P>This sample demonstrates how to expose a metadata endpoint that uses the relay 
    binding. MetadataExchange is supported in the following relay bindings: 
    NetTcpRelayBinding, NetOnewayRelayBinding, BasicHttpRelayBinding and 
    WS2007HttpRelayBinding.</P>
<H2 class=heading>Prerequisites</H2>
<DIV id=sectionSection0 class=section><content 
xmlns="http://ddue.schemas.microsoft.com/authoring/2003/5">
<P xmlns="">If you haven't already done so, please read the release notes 
document that explains how to sign up for a Windows Azure account and 
how to configure your environment.</P></content></DIV>
<H2 class=heading>Service</H2>
<DIV id=sectionSection1 class=section><content 
xmlns="http://ddue.schemas.microsoft.com/authoring/2003/5">
<P xmlns="">The service project is based on the service project in the 
<B>Echo</B> sample.</P>
<P xmlns="">To add metadata publishing to the service, modify the application 
configuration file to include an additional behavior section, as follows:</P>
<DIV class=code xmlns=""><SPAN codeLanguage="xml">
<TABLE cellSpacing=0 cellPadding=0 width="100%">
  <TBODY>
  <TR>
    <TH>Xml&nbsp;</TH>
</TR>
  <TR>
    <TD colSpan=2><PRE>&lt;behavior name="serviceMetadata"&gt;
  &lt;serviceMetadata /&gt;
&lt;/behavior&gt;</PRE></TD></TR></TBODY></TABLE></SPAN></DIV>
<P xmlns="">This behavior section is then referenced from the services 
configuration section:</P>
<DIV class=code xmlns=""><SPAN codeLanguage="xml">
<TABLE cellSpacing=0 cellPadding=0 width="100%">
  <TBODY>
  <TR>
    <TH>Xml&nbsp;</TH>
</TR>
  <TR>
    <TD colSpan=2><PRE class="style1">&lt;service name=&quot;Microsoft.ServiceBus.Samples.EchoService&quot;
   behaviorConfiguration=&quot;serviceMetadata&quot;&gt;
   &lt;endpoint name=&quot;RelayEndpoint&quot;
      contract=&quot;Microsoft.ServiceBus.Samples.IEchoContract&quot;
      binding=&quot;netTcpRelayBinding&quot; 
      address=&quot;&quot; /&gt;
   &lt;endpoint name=&quot;MexEndpoint&quot;
      contract=&quot;IMetadataExchange&quot;
      binding=&quot;ws2007HttpRelayBinding&quot; 
      bindingConfiguration=&quot;mexBinding&quot;
      address=&quot;&quot; /&gt;
&lt;/service&gt;</PRE></TD></TR></TBODY></TABLE></SPAN></DIV>

<P xmlns="">For the service to authenticate with the Service Bus, you are 
    prompted to enter the service namespace and the issuer credentials. The issuer 
    name is used to construct the service URI.
    <br />
    <br />
    Next, the sample creates a service endpoint and a MEX endpoint. It then adds the 
<CODE>TransportClientEndpointBehavior </CODE> and opens a service endpoint.</P>
<DIV class=code xmlns=""><SPAN codeLanguage="CSharp">
<TABLE cellSpacing=0 cellPadding=0 width="100%">
  <TBODY>
  <TR>
    <TH style="height: 17px">C#&nbsp;</TH>
</TR>
  <TR>
    <TD colSpan=2><PRE>Uri sbAddress = ServiceBusEnvironment.CreateServiceUri(&quot;sb&quot;, serviceNamespace, &quot;Echo/Service&quot;); 
Uri httpAddress = ServiceBusEnvironment.CreateServiceUri(&quot;http&quot;, serviceNamespace, &quot;Echo/mex&quot;);
...
TransportClientEndpointBehavior sharedSecretServiceBusCredential = new TransportClientEndpointBehavior();
sharedSecretServiceBusCredential.TokenProvider = TokenProvider.CreateSharedSecretTokenProvider(issuerName, issuerSecret);
...
ServiceHost host = new ServiceHost(typeof(EchoService), address);
...
host.Open();</PRE></TD></TR></TBODY></TABLE></SPAN></DIV>
    </content></DIV>
<H2 class=heading>Client</H2>
<DIV id=sectionSection2 class=section><content 
xmlns="http://ddue.schemas.microsoft.com/authoring/2003/5">
<P xmlns="">In this sample, the client is the Svcutil.exe tool. </P>
<TABLE cellSpacing=0 cellPadding=0 width="100%">
  <TBODY>
  <TR>
    <TH align=leftNote: </TH>&nbsp;</TR>
  <TR>
    <TD><b>Note:</b> Svcutil.exe is installed as part of the Windows SDK. A typical 
      location might be C:\Program Files\Microsoft 
      SDKs\Windows\&lt;version&gt;\Bin\NETFX 4.0 Tools\SvcUtil.exe</TD></TR></TBODY></TABLE>
    </content></DIV>

<H2 class=heading>Building and Running the Sample</H2>
<DIV id=sectionSection3 class=section><content 
xmlns="http://ddue.schemas.microsoft.com/authoring/2003/5">
<P xmlns="">To run the sample, build the solution in Visual Studio or from the 
command line. After building the solution, do the following to run 
the application.</P>
    <OL class=ordered xmlns="">
        <LI> From a command prompt, run the service (Service\bin\Debug\Service.exe) as 
            administrator.<LI> 
            Copy the svcutil.exe.config included with this solution in the same directory as 
            svcutil.exe. If svcutil.exe.config already exists, add the bindingExtensions, 
            policyImporters, and wsdlImporters of the attached svcutil.exe.config to it.</br>
        <LI>From another command prompt with elevated privileges, run the .NET 4.0 svcutil 
            against the mex endpoint opened by the service:
            <B>svcutil.exe 
        http://&lt;service-namespace&gt;.servicebus.windows.net/Echo/mex/</B>,
        replacing <B>&lt;service-namespace&gt;</B> with the your service namespace.<BR>
	</OL>

    <P xmlns=""><B>Expected Output – SvcUtill</B></P>
<DIV class=code xmlns=""><SPAN codeLanguage="other">
<TABLE cellSpacing=0 cellPadding=0 width="100%">
  <TBODY>
  <TR>
    <TH>&nbsp;</TH>
</TR>
  <TR>
    <TD colSpan=2><PRE>SvcUtil.exe /d:c:\ /r:"C:\Program Files\WindowsAzureSDK\v1.6\ServiceBus\ref\Microsoft.ServiceBus.dll" http://&lt;service-namespace&gt;.servicebus.windows.net/Echo/mex
Microsoft (R) Service Model Metadata Tool
[Microsoft .NET Framework, Version 3.0.50727.357]
Copyright (c) Microsoft Corporation.  All rights reserved.
 
Generating files...
C:\EchoService.cs
C:\output.config</PRE></TD></TR></TBODY></TABLE></SPAN></DIV>
<P xmlns=""><B>Expected Output – output.config</B></P>
<DIV class=code xmlns=""><SPAN codeLanguage="other">
<TABLE cellSpacing=0 cellPadding=0 width="100%">
  <TBODY>
  <TR>
    <TH>&nbsp;</TH>
</TR>
  <TR>
    <TD colSpan=2><PRE class="style1">&lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot;?&gt;
&lt;configuration&gt;  
   &lt;client&gt;
      &lt;endpoint address=&quot;sb://&lt;issuer-name&gt;.servicebus.windows.net/services/Echo/&quot;
         binding=&quot;netTcpRelayBinding&quot; bindingConfiguration=&quot;RelayEndpoint&quot;
         contract=&quot;IEchoContract&quot; name=&quot;RelayEndpoint&quot; /&gt;
      &lt;/client&gt;
   &lt;/system.serviceModel&gt;
&lt;/configuration&gt;</PRE></TD></TR></TBODY></TABLE></SPAN></DIV></content></DIV><!--[if gte IE 5]><tool:tip 
avoidmouse="false" element="languageFilterToolTip"></tool:tip><![endif]--></DIV>
<P xmlns="">
    <hr /> 
    Did you find this information useful?
    <a href="http://go.microsoft.com/fwlink/?LinkID=155664">
        Please send your suggestions and comments about the documentation.

    </a></P></DIV></BODY></HTML>
