// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace notifyevent
{
    using System;
    using System.Threading;
    using Bond.Comm;
    using Bond.Examples.NotifyEvent;

    public class NotifyEventService : NotifyEventServiceBase
    {
        const UInt16 MaxDelayMilliseconds = 2000;

        public override void NotifyAsync(IMessage<PingRequest> param)
        {
            PingRequest request = param.Payload.Deserialize();

            if (request.DelayMilliseconds > 0)
            {
                UInt16 delayMs = Math.Min(MaxDelayMilliseconds, request.DelayMilliseconds);
                Thread.Sleep(delayMs);
            }
            Console.WriteLine("Notified server-side, payload: " + request.Payload + " delay: " + request.DelayMilliseconds);
        }
    }
}