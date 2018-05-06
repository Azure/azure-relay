// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace pingpong
{
    using System;
    using Bond.Comm;

    public class ConsoleLogger : ILogSink
    {
        public void Log(string message, LogSeverity severity, Exception exception)
        {
            Console.WriteLine($"[bond] {severity.ToString().ToUpper()}: {message}");
            if (exception != null)
            {
                Console.WriteLine(exception);
            }
        }
    }
}