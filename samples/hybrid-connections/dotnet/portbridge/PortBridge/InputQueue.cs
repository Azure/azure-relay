// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridge
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;

    /// <summary>
    ///     Handles asynchronous interactions between producers and consumers.
    ///     Producers can dispatch available data to the input queue,
    ///     where it will be dispatched to a waiting consumer or stored until a
    ///     consumer becomes available. Consumers can synchronously or asynchronously
    ///     request data from the queue, which will be returned when data becomes
    ///     available.
    /// </summary>
    /// <typeparam name="T">The concrete type of the consumer objects that are waiting for data.</typeparam>
    public class InputQueue<T> : IDisposable where T : class
    {
        static WaitCallback onInvokeDequeuedCallback;
        static WaitCallback onDispatchCallback;
        static WaitCallback completeOutstandingReadersCallback;
        static WaitCallback completeWaitersFalseCallback;
        static WaitCallback completeWaitersTrueCallback;
        //Stores items that are waiting to be consumed.
        readonly ItemQueue itemQueue;
        //Each IQueueReader represents some consumer that is waiting for
        //items to appear in the queue. The readerQueue stores them
        //in an ordered list so consumers get serviced in a FIFO manner.
        readonly Queue<IQueueReader> readerQueue;
        //Each IQueueWaiter represents some waiter that is waiting for
        //items to appear in the queue.  When any item appears, all
        //waiters are signalled.
        readonly List<IQueueWaiter> waiterList;
        //Represents the current state of the InputQueue
        //as it transitions through its lifecycle.
        QueueState queueState;

        public InputQueue()
        {
            itemQueue = new ItemQueue();
            readerQueue = new Queue<IQueueReader>();
            waiterList = new List<IQueueWaiter>();
            queueState = QueueState.Open;
        }

        public int PendingCount
        {
            get
            {
                lock (ThisLock)
                {
                    return itemQueue.ItemCount;
                }
            }
        }

        object ThisLock
        {
            get { return itemQueue; }
        }

        public IAsyncResult BeginDequeue(TimeSpan timeout, AsyncCallback callback, object state)
        {
            Item item = default(Item);

            lock (ThisLock)
            {
                if (queueState == QueueState.Open)
                {
                    if (itemQueue.HasAvailableItem)
                    {
                        item = itemQueue.DequeueAvailableItem();
                    }
                    else
                    {
                        AsyncQueueReader reader = new AsyncQueueReader(this, timeout, callback, state);
                        readerQueue.Enqueue(reader);
                        return reader;
                    }
                }
                else if (queueState == QueueState.Shutdown)
                {
                    if (itemQueue.HasAvailableItem)
                    {
                        item = itemQueue.DequeueAvailableItem();
                    }
                    else if (itemQueue.HasAnyItem)
                    {
                        AsyncQueueReader reader = new AsyncQueueReader(this, timeout, callback, state);
                        readerQueue.Enqueue(reader);
                        return reader;
                    }
                }
            }

            InvokeDequeuedCallback(item.DequeuedCallback);
            return new TypedCompletedAsyncResult<T>(item.GetValue(), callback, state);
        }

        public IAsyncResult BeginWaitForItem(TimeSpan timeout, AsyncCallback callback, object state)
        {
            lock (ThisLock)
            {
                if (queueState == QueueState.Open)
                {
                    if (!itemQueue.HasAvailableItem)
                    {
                        AsyncQueueWaiter waiter = new AsyncQueueWaiter(timeout, callback, state);
                        waiterList.Add(waiter);
                        return waiter;
                    }
                }
                else if (queueState == QueueState.Shutdown)
                {
                    if (!itemQueue.HasAvailableItem && itemQueue.HasAnyItem)
                    {
                        AsyncQueueWaiter waiter = new AsyncQueueWaiter(timeout, callback, state);
                        waiterList.Add(waiter);
                        return waiter;
                    }
                }
            }

            return new TypedCompletedAsyncResult<bool>(true, callback, state);
        }

        static void CompleteOutstandingReadersCallback(object state)
        {
            IQueueReader[] outstandingReaders = (IQueueReader[])state;

            for (int i = 0; i < outstandingReaders.Length; i++)
            {
                outstandingReaders[i].Set(default(Item));
            }
        }

        static void CompleteWaitersFalseCallback(object state)
        {
            CompleteWaiters(false, (IQueueWaiter[])state);
        }

        static void CompleteWaitersTrueCallback(object state)
        {
            CompleteWaiters(true, (IQueueWaiter[])state);
        }

        static void CompleteWaiters(bool itemAvailable, IQueueWaiter[] waiters)
        {
            for (int i = 0; i < waiters.Length; i++)
            {
                waiters[i].Set(itemAvailable);
            }
        }

        static void CompleteWaitersLater(bool itemAvailable, IQueueWaiter[] waiters)
        {
            if (itemAvailable)
            {
                if (completeWaitersTrueCallback == null)
                {
                    completeWaitersTrueCallback = CompleteWaitersTrueCallback;
                }

                ThreadPool.QueueUserWorkItem(completeWaitersTrueCallback, waiters);
            }
            else
            {
                if (completeWaitersFalseCallback == null)
                {
                    completeWaitersFalseCallback = CompleteWaitersFalseCallback;
                }

                ThreadPool.QueueUserWorkItem(completeWaitersFalseCallback, waiters);
            }
        }

        void GetWaiters(out IQueueWaiter[] waiters)
        {
            if (waiterList.Count > 0)
            {
                waiters = waiterList.ToArray();
                waiterList.Clear();
            }
            else
            {
                waiters = null;
            }
        }

        public void Close()
        {
            this.Dispose();
        }

        public void Shutdown()
        {
            this.Shutdown(null);
        }

        // Don't let any more items in. Differs from Close in that we keep around
        // existing items in our itemQueue for possible future calls to Dequeue
        public void Shutdown(Func<Exception> pendingExceptionGenerator)
        {
            IQueueReader[] outstandingReaders = null;
            lock (ThisLock)
            {
                if (queueState == QueueState.Shutdown)
                {
                    return;
                }

                if (queueState == QueueState.Closed)
                {
                    return;
                }

                queueState = QueueState.Shutdown;

                if (readerQueue.Count > 0 && itemQueue.ItemCount == 0)
                {
                    outstandingReaders = new IQueueReader[readerQueue.Count];
                    readerQueue.CopyTo(outstandingReaders, 0);
                    readerQueue.Clear();
                }
            }

            if (outstandingReaders != null)
            {
                for (int i = 0; i < outstandingReaders.Length; i++)
                {
                    Exception exception = (pendingExceptionGenerator != null) ? pendingExceptionGenerator() : null;
                    outstandingReaders[i].Set(new Item(exception, null));
                }
            }
        }

        public T Dequeue(TimeSpan timeout)
        {
            T value;

            if (!Dequeue(timeout, out value))
            {
                throw new TimeoutException(string.Format("Dequeue timed out in {0}.", timeout));
            }

            return value;
        }

        public bool Dequeue(TimeSpan timeout, out T value)
        {
            WaitQueueReader reader = null;
            Item item = new Item();

            lock (ThisLock)
            {
                if (queueState == QueueState.Open)
                {
                    if (itemQueue.HasAvailableItem)
                    {
                        item = itemQueue.DequeueAvailableItem();
                    }
                    else
                    {
                        reader = new WaitQueueReader(this);
                        readerQueue.Enqueue(reader);
                    }
                }
                else if (queueState == QueueState.Shutdown)
                {
                    if (itemQueue.HasAvailableItem)
                    {
                        item = itemQueue.DequeueAvailableItem();
                    }
                    else if (itemQueue.HasAnyItem)
                    {
                        reader = new WaitQueueReader(this);
                        readerQueue.Enqueue(reader);
                    }
                    else
                    {
                        value = default(T);
                        return true;
                    }
                }
                else
                {
                    // queueState == QueueState.Closed
                    value = default(T);
                    return true;
                }
            }

            if (reader != null)
            {
                return reader.Wait(timeout, out value);
            }
            else
            {
                InvokeDequeuedCallback(item.DequeuedCallback);
                value = item.GetValue();
                return true;
            }
        }

        public void Dispose()
        {
            bool dispose = false;

            lock (ThisLock)
            {
                if (queueState != QueueState.Closed)
                {
                    queueState = QueueState.Closed;
                    dispose = true;
                }
            }

            if (dispose)
            {
                while (readerQueue.Count > 0)
                {
                    IQueueReader reader = readerQueue.Dequeue();
                    reader.Set(default(Item));
                }

                while (itemQueue.HasAnyItem)
                {
                    Item item = itemQueue.DequeueAnyItem();
                    DisposeItem(item);
                    InvokeDequeuedCallback(item.DequeuedCallback);
                }
            }
        }

        public void Dispatch()
        {
            IQueueReader reader = null;
            Item item = new Item();
            IQueueReader[] outstandingReaders = null;
            IQueueWaiter[] waiters = null;
            bool itemAvailable = true;

            lock (ThisLock)
            {
                itemAvailable = !((queueState == QueueState.Closed) || (queueState == QueueState.Shutdown));
                GetWaiters(out waiters);

                if (queueState != QueueState.Closed)
                {
                    itemQueue.MakePendingItemAvailable();

                    if (readerQueue.Count > 0)
                    {
                        item = itemQueue.DequeueAvailableItem();
                        reader = readerQueue.Dequeue();

                        if (queueState == QueueState.Shutdown && readerQueue.Count > 0 && itemQueue.ItemCount == 0)
                        {
                            outstandingReaders = new IQueueReader[readerQueue.Count];
                            readerQueue.CopyTo(outstandingReaders, 0);
                            readerQueue.Clear();

                            itemAvailable = false;
                        }
                    }
                }
            }

            if (outstandingReaders != null)
            {
                if (completeOutstandingReadersCallback == null)
                {
                    completeOutstandingReadersCallback = CompleteOutstandingReadersCallback;
                }

                ThreadPool.QueueUserWorkItem(completeOutstandingReadersCallback, outstandingReaders);
            }

            if (waiters != null)
            {
                CompleteWaitersLater(itemAvailable, waiters);
            }

            if (reader != null)
            {
                InvokeDequeuedCallback(item.DequeuedCallback);
                reader.Set(item);
            }
        }

        //Ends an asynchronous Dequeue operation.
        public T EndDequeue(IAsyncResult result)
        {
            T value;

            if (!EndDequeue(result, out value))
            {
                throw new TimeoutException("Asynchronous Dequeue operation timed out.");
            }

            return value;
        }

        public bool EndDequeue(IAsyncResult result, out T value)
        {
            TypedCompletedAsyncResult<T> typedResult = result as TypedCompletedAsyncResult<T>;

            if (typedResult != null)
            {
                value = TypedCompletedAsyncResult<T>.End(result);
                return true;
            }

            return AsyncQueueReader.End(result, out value);
        }

        public bool EndWaitForItem(IAsyncResult result)
        {
            TypedCompletedAsyncResult<bool> typedResult = result as TypedCompletedAsyncResult<bool>;
            if (typedResult != null)
            {
                return TypedCompletedAsyncResult<bool>.End(result);
            }

            return AsyncQueueWaiter.End(result);
        }

        public void EnqueueAndDispatch(T item)
        {
            EnqueueAndDispatch(item, null);
        }

        // dequeuedCallback is called as an item is dequeued from the InputQueue.  The 
        // InputQueue lock is not held during the callback.  However, the user code will
        // not be notified of the item being available until the callback returns.  If you
        // are not sure if the callback will block for a long time, then first call 
        // IOThreadScheduler.ScheduleCallback to get to a "safe" thread.
        public void EnqueueAndDispatch(T item, Action dequeuedCallback)
        {
            EnqueueAndDispatch(item, dequeuedCallback, true);
        }

        public void EnqueueAndDispatch(Exception exception, Action dequeuedCallback, bool canDispatchOnThisThread)
        {
            Debug.Assert(exception != null, "EnqueueAndDispatch: exception parameter should not be null");
            EnqueueAndDispatch(new Item(exception, dequeuedCallback), canDispatchOnThisThread);
        }

        public void EnqueueAndDispatch(T item, Action dequeuedCallback, bool canDispatchOnThisThread)
        {
            Debug.Assert(item != null, "EnqueueAndDispatch: item parameter should not be null");
            EnqueueAndDispatch(new Item(item, dequeuedCallback), canDispatchOnThisThread);
        }

        void EnqueueAndDispatch(Item item, bool canDispatchOnThisThread)
        {
            bool disposeItem = false;
            IQueueReader reader = null;
            bool dispatchLater = false;
            IQueueWaiter[] waiters = null;
            bool itemAvailable = true;

            lock (ThisLock)
            {
                itemAvailable = !((queueState == QueueState.Closed) || (queueState == QueueState.Shutdown));
                GetWaiters(out waiters);

                if (queueState == QueueState.Open)
                {
                    if (canDispatchOnThisThread)
                    {
                        if (readerQueue.Count == 0)
                        {
                            itemQueue.EnqueueAvailableItem(item);
                        }
                        else
                        {
                            reader = readerQueue.Dequeue();
                        }
                    }
                    else
                    {
                        if (readerQueue.Count == 0)
                        {
                            itemQueue.EnqueueAvailableItem(item);
                        }
                        else
                        {
                            itemQueue.EnqueuePendingItem(item);
                            dispatchLater = true;
                        }
                    }
                }
                else
                {
                    // queueState == QueueState.Closed || queueState == QueueState.Shutdown
                    disposeItem = true;
                }
            }

            if (waiters != null)
            {
                if (canDispatchOnThisThread)
                {
                    CompleteWaiters(itemAvailable, waiters);
                }
                else
                {
                    CompleteWaitersLater(itemAvailable, waiters);
                }
            }

            if (reader != null)
            {
                InvokeDequeuedCallback(item.DequeuedCallback);
                reader.Set(item);
            }

            if (dispatchLater)
            {
                if (onDispatchCallback == null)
                {
                    onDispatchCallback = OnDispatchCallback;
                }

                ThreadPool.QueueUserWorkItem(onDispatchCallback, this);
            }
            else if (disposeItem)
            {
                InvokeDequeuedCallback(item.DequeuedCallback);
                DisposeItem(item);
            }
        }

        public bool EnqueueWithoutDispatch(T item, Action dequeuedCallback)
        {
            Debug.Assert(item != null, "EnqueueWithoutDispatch: item parameter should not be null");
            return EnqueueWithoutDispatch(new Item(item, dequeuedCallback));
        }

        public bool EnqueueWithoutDispatch(Exception exception, Action dequeuedCallback)
        {
            Debug.Assert(exception != null, "EnqueueWithoutDispatch: exception parameter should not be null");
            return EnqueueWithoutDispatch(new Item(exception, dequeuedCallback));
        }

        // This will not block, however, Dispatch() must be called later if this function
        // returns true.
        bool EnqueueWithoutDispatch(Item item)
        {
            lock (ThisLock)
            {
                // Open
                if (queueState != QueueState.Closed && queueState != QueueState.Shutdown)
                {
                    if (readerQueue.Count == 0 && waiterList.Count == 0)
                    {
                        itemQueue.EnqueueAvailableItem(item);
                        return false;
                    }
                    else
                    {
                        itemQueue.EnqueuePendingItem(item);
                        return true;
                    }
                }
            }

            DisposeItem(item);
            InvokeDequeuedCallbackLater(item.DequeuedCallback);
            return false;
        }

        static void OnDispatchCallback(object state)
        {
            ((InputQueue<T>)state).Dispatch();
        }

        static void InvokeDequeuedCallbackLater(Action dequeuedCallback)
        {
            if (dequeuedCallback != null)
            {
                if (onInvokeDequeuedCallback == null)
                {
                    onInvokeDequeuedCallback = OnInvokeDequeuedCallback;
                }

                ThreadPool.QueueUserWorkItem(onInvokeDequeuedCallback, dequeuedCallback);
            }
        }

        static void InvokeDequeuedCallback(Action dequeuedCallback)
        {
            if (dequeuedCallback != null)
            {
                dequeuedCallback();
            }
        }

        static void OnInvokeDequeuedCallback(object state)
        {
            Debug.Assert(state != null, "InputQueue.OnInvokeDequeuedCallback: (state != null)");

            Action dequeuedCallback = (Action)state;
            dequeuedCallback();
        }

        // Used for timeouts. The InputQueue must remove readers from its reader queue to prevent
        // dispatching items to timed out readers.
        bool RemoveReader(IQueueReader reader)
        {
            Debug.Assert(reader != null, "InputQueue.RemoveReader: (reader != null)");

            lock (ThisLock)
            {
                if (queueState == QueueState.Open || queueState == QueueState.Shutdown)
                {
                    bool removed = false;

                    for (int i = readerQueue.Count; i > 0; i--)
                    {
                        IQueueReader temp = readerQueue.Dequeue();
                        if (object.ReferenceEquals(temp, reader))
                        {
                            removed = true;
                        }
                        else
                        {
                            readerQueue.Enqueue(temp);
                        }
                    }

                    return removed;
                }
            }

            return false;
        }

        public bool WaitForItem(TimeSpan timeout)
        {
            WaitQueueWaiter waiter = null;
            bool itemAvailable = false;

            lock (ThisLock)
            {
                if (queueState == QueueState.Open)
                {
                    if (itemQueue.HasAvailableItem)
                    {
                        itemAvailable = true;
                    }
                    else
                    {
                        waiter = new WaitQueueWaiter();
                        waiterList.Add(waiter);
                    }
                }
                else if (queueState == QueueState.Shutdown)
                {
                    if (itemQueue.HasAvailableItem)
                    {
                        itemAvailable = true;
                    }
                    else if (itemQueue.HasAnyItem)
                    {
                        waiter = new WaitQueueWaiter();
                        waiterList.Add(waiter);
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    // queueState == QueueState.Closed
                    return true;
                }
            }

            if (waiter != null)
            {
                return waiter.Wait(timeout);
            }
            else
            {
                return itemAvailable;
            }
        }

        void DisposeItem(Item item)
        {
            T value = item.Value;
            if (value != null)
            {
                IDisposable disposableValue = value as IDisposable;
                if (disposableValue != null)
                {
                    disposableValue.Dispose();
                }
            }
        }

        class AsyncQueueReader : AsyncResult, IQueueReader
        {
            static readonly TimerCallback timerCallback = TimerCallback;
            readonly InputQueue<T> inputQueue;
            readonly Timer timer;
            bool expired;
            T item;

            public AsyncQueueReader(InputQueue<T> inputQueue, TimeSpan timeout, AsyncCallback callback, object state)
                : base(callback, state)
            {
                this.inputQueue = inputQueue;
                if (timeout != TimeSpan.MaxValue)
                {
                    timer = new Timer(timerCallback, this, Timeout.Infinite, Timeout.Infinite);
                    timer.Change(timeout, Timeout.InfiniteTimeSpan);
                }
            }

            public void Set(Item inputItem)
            {
                this.item = inputItem.Value;
                if (this.timer != null)
                {
                    this.timer.Change(Timeout.Infinite, Timeout.Infinite);
                }
                Complete(false, inputItem.Exception);
            }

            public static bool End(IAsyncResult result, out T value)
            {
                AsyncQueueReader readerResult = AsyncResult.End<AsyncQueueReader>(result);

                if (readerResult.expired)
                {
                    value = default(T);
                    return false;
                }
                else
                {
                    value = readerResult.item;
                    return true;
                }
            }

            static void TimerCallback(object state)
            {
                AsyncQueueReader thisPtr = (AsyncQueueReader)state;
                if (thisPtr.inputQueue.RemoveReader(thisPtr))
                {
                    thisPtr.expired = true;
                    thisPtr.Complete(false);
                }
            }
        }

        class AsyncQueueWaiter : AsyncResult, IQueueWaiter
        {
            static readonly TimerCallback timerCallback = TimerCallback;
            readonly Timer timer;
            bool itemAvailable;

            public AsyncQueueWaiter(TimeSpan timeout, AsyncCallback callback, object state) : base(callback, state)
            {
                if (timeout != TimeSpan.MaxValue)
                {
                    timer = new Timer(timerCallback, this, Timeout.Infinite, Timeout.Infinite);
                    timer.Change(timeout, Timeout.InfiniteTimeSpan);
                }
            }

            public void Set(bool currentItemAvailable)
            {
                bool timely;

                lock (ThisLock)
                {
                    if (this.timer == null)
                    {
                        timely = true;
                    }
                    else
                    {
                        this.timer.Change(Timeout.Infinite, Timeout.Infinite);
                        timely = !this.IsCompleted;
                    }
                    this.itemAvailable = currentItemAvailable;
                }

                if (timely)
                {
                    Complete(false);
                }
            }

            public static bool End(IAsyncResult result)
            {
                AsyncQueueWaiter waiterResult = End<AsyncQueueWaiter>(result);
                return waiterResult.itemAvailable;
            }

            static void TimerCallback(object state)
            {
                AsyncQueueWaiter thisPtr = (AsyncQueueWaiter)state;
                thisPtr.Complete(false);
            }
        }

        interface IQueueReader
        {
            void Set(Item item);
        }

        interface IQueueWaiter
        {
            void Set(bool itemAvailable);
        }

        enum QueueState
        {
            Open,
            Shutdown,
            Closed
        }

        class WaitQueueReader : IQueueReader, IDisposable
        {
            readonly InputQueue<T> inputQueue;
            readonly ManualResetEvent waitEvent;
            Exception exception;
            T item;

            public WaitQueueReader(InputQueue<T> inputQueue)
            {
                this.inputQueue = inputQueue;
                waitEvent = new ManualResetEvent(false);
            }

            object ThisLock => this.waitEvent;

            public void Set(Item newItem)
            {
                lock (ThisLock)
                {
                    Debug.Assert(this.item == null, "InputQueue.WaitQueueReader.Set: (this.item == null)");
                    Debug.Assert(exception == null, "InputQueue.WaitQueueReader.Set: (this.exception == null)");

                    this.exception = newItem.Exception;
                    this.item = newItem.Value;
                    waitEvent.Set();
                }
            }

            public bool Wait(TimeSpan timeout, out T value)
            {
                bool isSafeToClose = false;
                try
                {
                    if (timeout == TimeSpan.MaxValue)
                    {
                        waitEvent.WaitOne();
                    }
                    else if (!waitEvent.WaitOne(timeout, false))
                    {
                        if (inputQueue.RemoveReader(this))
                        {
                            value = default(T);
                            isSafeToClose = true;
                            return false;
                        }
                        else
                        {
                            waitEvent.WaitOne();
                        }
                    }

                    isSafeToClose = true;
                }
                finally
                {
                    if (isSafeToClose)
                    {
                        waitEvent.Close();
                    }
                }

                if (this.exception != null)
                {
                    throw this.exception;
                }

                value = item;
                return true;
            }

            public void Dispose()
            {
                this.waitEvent.Dispose();
                GC.SuppressFinalize(this);
            }
        }

        class WaitQueueWaiter : IQueueWaiter, IDisposable
        {
            readonly ManualResetEvent waitEvent;
            bool itemAvailable;

            public WaitQueueWaiter()
            {
                waitEvent = new ManualResetEvent(false);
            }

            object ThisLock => this.waitEvent;

            public void Set(bool itemAvailable)
            {
                lock (ThisLock)
                {
                    this.itemAvailable = itemAvailable;
                    waitEvent.Set();
                }
            }

            public bool Wait(TimeSpan timeout)
            {
                if (timeout == TimeSpan.MaxValue)
                {
                    waitEvent.WaitOne();
                }
                else if (!waitEvent.WaitOne(timeout, false))
                {
                    return false;
                }

                return itemAvailable;
            }

            public void Dispose()
            {
                this.waitEvent.Close();
                GC.SuppressFinalize(this);
            }
        }

        struct Item
        {
            Action dequeuedCallback;
            Exception exception;
            T value;

            public Item(T value, Action dequeuedCallback)
                : this(value, null, dequeuedCallback)
            {
            }

            public Item(Exception exception, Action dequeuedCallback)
                : this(null, exception, dequeuedCallback)
            {
            }

            Item(T value, Exception exception, Action dequeuedCallback)
            {
                this.value = value;
                this.exception = exception;
                this.dequeuedCallback = dequeuedCallback;
            }

            public Exception Exception => this.exception;
            public T Value => this.value;
            public Action DequeuedCallback => dequeuedCallback;

            public T GetValue()
            {
                if (this.exception != null)
                {
                    throw this.exception;
                }

                return this.value;
            }
        }

        class ItemQueue
        {
            int head;
            Item[] items;
            int pendingCount;
            int totalCount;

            public ItemQueue()
            {
                this.items = new Item[1];
            }

            public bool HasAvailableItem
            {
                get { return this.totalCount > this.pendingCount; }
            }

            public bool HasAnyItem
            {
                get { return this.totalCount > 0; }
            }

            public int ItemCount
            {
                get { return this.totalCount; }
            }

            public Item DequeueAvailableItem()
            {
                if (ItemCount == pendingCount)
                {
                    Debug.Assert(false, "ItemQueue does not contain any available items");
                    throw new Exception("Internal Error");
                }

                return DequeueItemCore();
            }

            public Item DequeueAnyItem()
            {
                if (this.pendingCount == this.totalCount)
                {
                    this.pendingCount--;
                }
                return DequeueItemCore();
            }

            void EnqueueItemCore(Item item)
            {
                if (this.totalCount == this.items.Length)
                {
                    Item[] newItems = new Item[this.items.Length * 2];
                    for (int i = 0; i < this.totalCount; i++)
                    {
                        newItems[i] = this.items[(head + i) % this.items.Length];
                    }
                    this.head = 0;
                    this.items = newItems;
                }
                int tail = (this.head + this.totalCount) % this.items.Length;
                this.items[tail] = item;
                this.totalCount++;
            }

            Item DequeueItemCore()
            {
                if (ItemCount == 0)
                {
                    Debug.Assert(false, "ItemQueue does not contain any items");
                    throw new Exception("Internal Error");
                }

                Item item = this.items[this.head];
                this.items[this.head] = new Item();
                this.totalCount--;
                this.head = (this.head + 1) % this.items.Length;
                return item;
            }

            public void EnqueuePendingItem(Item item)
            {
                EnqueueItemCore(item);
                this.pendingCount++;
            }

            public void EnqueueAvailableItem(Item item)
            {
                EnqueueItemCore(item);
            }

            public void MakePendingItemAvailable()
            {
                if (pendingCount == 0)
                {
                    Debug.Assert(false, "ItemQueue does not contain any pending items");
                    throw new Exception("Internal Error");
                }
                this.pendingCount--;
            }
        }
    }
}