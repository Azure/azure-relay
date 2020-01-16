// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridge
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;

    public class MultiplexConnectionInputPump
    {
        readonly BufferRead bufferRead;
        readonly object callbackState;
        readonly MultiplexConnectionFactoryHandler connectionFactory;
        readonly object connectionLock = new object();
        readonly Dictionary<int, MultiplexedConnection> connections;
        readonly byte[] inputBuffer;
        readonly byte[] preambleBuffer;
        readonly ManualResetEvent stopInput;
        bool closed;
        public EventHandler Completed;
        bool stopped;

        public MultiplexConnectionInputPump(BufferRead bufferRead, MultiplexConnectionFactoryHandler connectionFactory, object callbackState)
        {
            this.callbackState = callbackState;
            this.bufferRead = bufferRead;
            this.connectionFactory = connectionFactory;
            connections = new Dictionary<int, MultiplexedConnection>();
            inputBuffer = new byte[65536];
            // Each frame is prefixed with a 32-bit connectionId and a 16-bit length value.
            preambleBuffer = new byte[sizeof(int) + sizeof(ushort)];
            stopInput = new ManualResetEvent(false);
        }

        public virtual void Close()
        {
            if (!closed)
            {
                closed = stopped = true;
                stopInput.Set();
            }
        }

        public void Run()
        {
            Run(true);
        }

        public void Run(bool completeSynchronously)
        {
            // read from delegate
            bufferRead.BeginInvoke(preambleBuffer, 0, preambleBuffer.Length, DoneReadingPreamble, null);
            if (completeSynchronously)
            {
                stopInput.WaitOne();
            }
        }

        public void DoneReadingPreamble(IAsyncResult readOutputAsyncResult)
        {
            try
            {
                int bytesRead = bufferRead.EndInvoke(readOutputAsyncResult);
                if (bytesRead > 0)
                {
                    if (bytesRead < preambleBuffer.Length)
                    {
                        // In case read returned a partial frame preamble ensure that the full 6 bytes are received
                        bytesRead += this.ReadCountBytes(preambleBuffer, bytesRead, preambleBuffer.Length - bytesRead, "Frame Preamble");
                    }
#if VERBOSE
                    Trace.TraceInformation("Input, read preamble: {0}", bytesRead);
#endif

                    int connectionId = BitConverter.ToInt32(preambleBuffer, 0);
                    ushort frameSize = BitConverter.ToUInt16(preambleBuffer, sizeof(Int32));

                    // we have to get the frame off the wire irrespective of 
                    // whether we can dispatch it
                    if (frameSize > 0)
                    {
                        // read the block synchronously
                        bytesRead = this.ReadCountBytes(inputBuffer, 0, frameSize, "Frame Payload");
#if VERBOSE
                        Trace.TraceInformation("Input, read data {0}", frameSize);
#endif
                    }

                    MultiplexedConnection connection;

                    lock (connectionLock)
                    {
                        if (!connections.TryGetValue(connectionId, out connection))
                        {
                            try
                            {
                                connection = connectionFactory(connectionId, callbackState);
                                if (connection != null)
                                {
                                    connections.Add(connectionId, connection);
                                }
                            }
                            catch (Exception ex)
                            {
                                Trace.TraceError("Unable to establish multiplexed connection: {0}", ex.Message);
                                connection = null;
                            }
                        }
                    }

                    if (connection != null)
                    {
                        bool shutdownConnection = (frameSize == 0);
                        if (frameSize > 0)
                        {
                            try
                            {
                                connection.Write(inputBuffer, 0, frameSize);
#if VERBOSE
                                Trace.TraceInformation("Connection write: {0}", frameSize);
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
                                    Trace.TraceError("Unable to write to multiplexed connection: {0}", ioe.Message);
                                }
                                shutdownConnection = true;
                            }
                            catch (Exception ex)
                            {
                                Trace.TraceError("Unable to write to multiplexed connection: {0}", ex.Message);
                                shutdownConnection = true;
                            }
                        }

                        if (shutdownConnection)
                        {
                            connection.Dispose();
                            lock (connectionLock)
                            {
                                if (connections.ContainsKey(connectionId))
                                {
                                    connections.Remove(connectionId);
                                }
                            }
                        }
                    }

                    if (!stopped)
                    {
                        bufferRead.BeginInvoke(preambleBuffer, 0, preambleBuffer.Length, DoneReadingPreamble, null);
                    }
                }
                else
                {
                    OnCompleted();
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error starting multiplex pump : {0}", ex.Message);
                OnCompleted();
            }
        }

        /// <summary>
        /// Reads the given count of bytes from the bufferRead delegate into the given buffer.
        /// A ProtocolViolationException will be thrown if the stream read returns 0.
        /// </summary>
        /// <param name="buffer">The destination buffer that bytes get copied to</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data.</param>
        /// <param name="requiredCount">The required number of bytes to be read from the current bufferRead delegate.</param>
        /// <param name="stepName">The name of the step for reporting if reading unexpectedly returns 0 bytes.</param>
        /// <returns>The count of bytes read for programming convenience. Will always be the same as <paramref name="requiredCount"/>.</returns>
        int ReadCountBytes(byte[] buffer, int offset, int requiredCount, string stepName)
        {
            int totalBytesRead = 0;
            do
            {
                int bytesRead = this.bufferRead(buffer, offset + totalBytesRead, requiredCount - totalBytesRead);
                totalBytesRead += bytesRead;
                if (bytesRead == 0)
                {
                    throw new ProtocolViolationException($"Unexpected end of stream while reading {stepName}.");
                }
            }
            while (totalBytesRead < requiredCount);

            return totalBytesRead;
        }

        void OnCompleted()
        {
            Close();
            this.Completed?.Invoke(this, EventArgs.Empty);
        }
    }

    public delegate MultiplexedConnection MultiplexConnectionFactoryHandler(int connectionId, object state);
}