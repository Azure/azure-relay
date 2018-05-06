// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Server
{
    using System;
    using System.Collections.Generic;

    public class CalculatorHandler : Calculator.Iface
    {
        readonly Dictionary<int, SharedStruct> log;

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
                    val = work.Num1*work.Num2;
                    break;

                case Operation.DIVIDE:
                    if (work.Num2 == 0)
                    {
                        InvalidOperation io = new InvalidOperation();
                        io.WhatOp = (int) work.Op;
                        io.Why = "Cannot divide by 0";
                        throw io;
                    }
                    val = work.Num1/work.Num2;
                    break;

                default:
                {
                    InvalidOperation io = new InvalidOperation();
                    io.WhatOp = (int) work.Op;
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
}