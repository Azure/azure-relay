// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridge
{
    using System;
    using System.Net;
    using System.Net.Sockets;

    public class IPRange
    {
        readonly long begin;
        readonly long end;

        public IPRange(IPAddress address)
        {
            if (address.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("only IPv4 addresses permitted", "address");
            }
            begin = end = IPAddressToInt(address);
        }

        public IPRange(IPAddress begin, IPAddress end)
        {
            if (begin.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("only IPv4 addresses permitted", "begin");
            }
            if (end.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("only IPv4 addresses permitted", "end");
            }
            this.begin = IPAddressToInt(begin);
            this.end = IPAddressToInt(end);
        }

        public bool IsInRange(IPAddress address)
        {
            long ad = IPAddressToInt(address);
            return (begin <= ad && end >= ad);
        }

        long IPAddressToInt(IPAddress address)
        {
            byte[] ab = address.GetAddressBytes();
            long result = (((long)ab[0] << 24) + ((long)ab[1] << 16) + ((long)ab[2] << 8) + ab[3]);
            return result;
        }
    }
}