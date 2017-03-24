// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridge
{
    public delegate int BufferRead(byte[] buffer, int offset, int count);

    public delegate void BufferWrite(byte[] buffer, int offset, int count);
}