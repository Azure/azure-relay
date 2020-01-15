// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridge
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Sockets;

    public class MultiplexConnectionOutputPump : Pump
    {
        const int preambleSize = sizeof (int) + sizeof (ushort);
        readonly BufferRead bufferRead;
        readonly BufferWrite bufferWrite;
        readonly int connectionId;
        readonly byte[] inputBuffer;
        readonly object threadLock = new object();

        public MultiplexConnectionOutputPump(BufferRead bufferRead, BufferWrite bufferWrite, int connectionId)
        {
            this.bufferRead = bufferRead;
            this.bufferWrite = bufferWrite;
            inputBuffer = new byte[65536];
            this.connectionId = connectionId;
        }

        public override IAsyncResult BeginRunPump(AsyncCallback callback, object state)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("Already running");
            }
            IsRunning = true;

            Caller = new PumpAsyncResult(callback, state);
            bufferRead.BeginInvoke(inputBuffer, preambleSize, inputBuffer.Length - preambleSize, DoneReading, null);
            return Caller;
        }

        public void DoneReading(IAsyncResult readOutputAsyncResult)
        {
            int bytesRead;
            try
            {
                try
                {
                    bytesRead = bufferRead.EndInvoke(readOutputAsyncResult);
#if VERBOSE
                    Trace.TraceInformation("Output, read bytes: {0}", bytesRead);
#endif
                }
                catch (IOException ioe)
                {
                    if (ioe.InnerException is SocketException &&
                        (((SocketException) ioe.InnerException).ErrorCode == 10004 ||
                         ((SocketException) ioe.InnerException).ErrorCode == 10054))
                    {
                        Trace.TraceInformation(
                            "Socket cancelled with code {0} during pending read: {1}",
                            ((SocketException) ioe.InnerException).SocketErrorCode,
                            ioe.Message);
                    }
                    else
                    {
                        Trace.TraceError("Unable to read from source: {0}", ioe.Message);
                    }
                    bytesRead = 0;
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Unable to read from source: {0}", ex.Message);
                    bytesRead = 0;
                }

                if (bytesRead > 0)
                {
                    lock (threadLock)
                    {
                        byte[] connectionIdPreamble = BitConverter.GetBytes(connectionId);
                        Buffer.BlockCopy(connectionIdPreamble, 0, inputBuffer, 0, sizeof (int));
                        byte[] sizePreamble = BitConverter.GetBytes((ushort) bytesRead);
                        Buffer.BlockCopy(sizePreamble, 0, inputBuffer, sizeof (int), sizeof (ushort));

                        bufferWrite(inputBuffer, 0, bytesRead + preambleSize);
#if VERBOSE
                        Trace.TraceInformation("Output, wrote preamble: {0}", bytesRead + preambleSize);
#endif
                    }

                    if (!IsClosed)
                    {
                        try
                        {
                            bufferRead.BeginInvoke(inputBuffer, preambleSize, inputBuffer.Length - preambleSize, DoneReading, null);
                        }
                        catch (Exception ex)
                        {
                            Trace.TraceError("Can't start reading from source: {0}", ex.Message);
                            SetComplete(ex);
                        }
                    }
                }
                else
                {
                    lock (threadLock)
                    {
                        byte[] connectionIdPreamble = BitConverter.GetBytes(connectionId);
                        Buffer.BlockCopy(connectionIdPreamble, 0, inputBuffer, 0, sizeof (int));
                        byte[] sizePreamble = BitConverter.GetBytes((ushort) 0);
                        Buffer.BlockCopy(sizePreamble, 0, inputBuffer, sizeof (int), sizeof (ushort));

                        bufferWrite(inputBuffer, 0, preambleSize);
#if VERBOSE
                        Trace.TraceInformation("Output, wrote preamble: {0}", bytesRead + preambleSize);
#endif
                    }
                    SetComplete();
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Unable to write to target: {0}", ex.Message);
                SetComplete(ex);
            }
        }
    }
}