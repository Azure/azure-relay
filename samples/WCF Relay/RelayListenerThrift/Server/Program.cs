using System;
using System.Collections.Generic;
using Thrift.Server;
using Thrift.Transport;

// Adapted http://thrift.apache.org/tutorial/csharp

namespace RelaySamples
{
    using System.Threading.Tasks;
    using AzureServiceBus.RelayListener;
    using Microsoft.ServiceBus;

    public class CalculatorHandler : Calculator.Iface
    {
        Dictionary<int, SharedStruct> log;

        public CalculatorHandler()
        {
            log = new Dictionary<int, SharedStruct>();
        }

        public void ping()
        {
            Console.WriteLine("ping()");
        }

        public int add(int n1, int n2)
        {
            Console.WriteLine("add({0},{1})", n1, n2);
            return n1 + n2;
        }

        public int calculate(int logid, Work work)
        {
            Console.WriteLine("calculate({0}, [{1},{2},{3}])", logid, work.Op, work.Num1, work.Num2);
            int val = 0;
            switch (work.Op)
            {
                case Operation.ADD:
                    val = work.Num1 + work.Num2;
                    break;

                case Operation.SUBTRACT:
                    val = work.Num1 - work.Num2;
                    break;

                case Operation.MULTIPLY:
                    val = work.Num1 * work.Num2;
                    break;

                case Operation.DIVIDE:
                    if (work.Num2 == 0)
                    {
                        InvalidOperation io = new InvalidOperation();
                        io.WhatOp = (int)work.Op;
                        io.Why = "Cannot divide by 0";
                        throw io;
                    }
                    val = work.Num1 / work.Num2;
                    break;

                default:
                    {
                        InvalidOperation io = new InvalidOperation();
                        io.WhatOp = (int)work.Op;
                        io.Why = "Unknown operation";
                        throw io;
                    }
            }

            SharedStruct entry = new SharedStruct();
            entry.Key = logid;
            entry.Value = val.ToString();
            log[logid] = entry;

            return val;
        }

        public SharedStruct getStruct(int key)
        {
            Console.WriteLine("getStruct({0})", key);
            return log[key];
        }

        public void zip()
        {
            Console.WriteLine("zip()");
        }
    }

    public class RelayListenerServerTransport : TServerTransport
    {
        readonly RelayListener listener;

        public RelayListenerServerTransport(RelayListener listener)
        {
            this.listener = listener;
        }

        public override void Listen()
        {
            this.listener.StartAsync().GetAwaiter().GetResult();
        }

        public override void Close()
        {
            this.listener.Stop();
        }

        protected override TTransport AcceptImpl()
        {
            var acceptedConnection = this.listener.AcceptConnection(TimeSpan.MaxValue);
            return new TStreamTransport(acceptedConnection, acceptedConnection);
        }
    }

    public class Program : ITcpListenerSample
    {
        public async Task Run(string listenAddress, string listenToken)
        {
            try
            {
                CalculatorHandler handler = new CalculatorHandler();
                Calculator.Processor processor = new Calculator.Processor(handler);

                TServerTransport serverTransport = new RelayListenerServerTransport(
                   new RelayListener(listenAddress,
                    TokenProvider.CreateSharedAccessSignatureTokenProvider(listenToken),
                    RelayAddressType.Configured));

                TServer server = new TSimpleServer(processor, serverTransport);

                // Use this for a multithreaded server
                // server = new TThreadPoolServer(processor, serverTransport);

                Console.WriteLine("Starting the server...");
                server.Serve();
            }
            catch (Exception x)
            {
                Console.WriteLine(x.StackTrace);
            }
            Console.WriteLine("done.");
        }


    }
}