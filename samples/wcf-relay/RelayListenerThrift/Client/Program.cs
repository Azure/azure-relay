using System;
using Thrift;
using Thrift.Protocol;
using Thrift.Server;
using Thrift.Transport;

// Adapted http://thrift.apache.org/tutorial/csharp

namespace RelaySamples
{
    using System.Threading.Tasks;
    using AzureServiceBus.RelayListener;
    using Microsoft.ServiceBus;

    public class Program : RelaySamples.ITcpSenderSample
    {
        public async Task Run(string sendAddress, string sendToken)
        {
            try
            {
                var relayClient = new RelayClient(sendAddress, TokenProvider.CreateSharedAccessSignatureTokenProvider(sendToken));
                var relayConnection = relayClient.Connect();

                TTransport transport = new TStreamTransport(relayConnection, relayConnection);
                TProtocol protocol = new TBinaryProtocol(transport);
                Calculator.Client client = new Calculator.Client(protocol);

                transport.Open();
                try
                {
                    client.ping();
                    Console.WriteLine("ping()");

                    int sum = client.add(1, 1);
                    Console.WriteLine("1+1={0}", sum);

                    Work work = new Work();

                    work.Op = Operation.DIVIDE;
                    work.Num1 = 1;
                    work.Num2 = 0;
                    try
                    {
                        int quotient = client.calculate(1, work);
                        Console.WriteLine("Whoa we can divide by 0");
                    }
                    catch (InvalidOperation io)
                    {
                        Console.WriteLine("Invalid operation: " + io.Why);
                    }

                    work.Op = Operation.SUBTRACT;
                    work.Num1 = 15;
                    work.Num2 = 10;
                    try
                    {
                        int diff = client.calculate(1, work);
                        Console.WriteLine("15-10={0}", diff);
                    }
                    catch (InvalidOperation io)
                    {
                        Console.WriteLine("Invalid operation: " + io.Why);
                    }

                    SharedStruct log = client.getStruct(1);
                    Console.WriteLine("Check log: {0}", log.Value);

                }
                finally
                {
                    transport.Close();
                }
            }
            catch (TApplicationException x)
            {
                Console.WriteLine(x.StackTrace);
            }

        }

        
    }
}