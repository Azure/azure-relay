// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;

    class AsyncLock : IDisposable
    {
        readonly SemaphoreSlim asyncSemaphore;
        readonly Task<LockRelease> lockRelease;
        bool disposed;

        public AsyncLock()
        {
            this.asyncSemaphore = new SemaphoreSlim(1);
            this.lockRelease = Task.FromResult(new LockRelease(this));
        }

        public Task<LockRelease> LockAsync()
        {
            var wait = this.asyncSemaphore.WaitAsync();
            if (wait.IsCompleted)
            {
                return this.lockRelease;
            }

            return wait.ContinueWith(
                (_, state) => new LockRelease((AsyncLock)state),
                this,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public Task<LockRelease> LockAsync(CancellationToken cancellationToken)
        {
            var wait = this.asyncSemaphore.WaitAsync(cancellationToken);

            // Note this check is on RanToCompletion; not IsCompleted which could be RanToCompletion, Faulted, or Canceled.
            if (wait.Status == TaskStatus.RanToCompletion)
            {
                return this.lockRelease;
            }

            // Since we pass the cancellationToken here if it gets cancelled the task returned from 
            // ContinueWith will itself be Cancelled.
            return wait.ContinueWith(
                (t, state) =>
                {
                    if (t.IsFaulted)
                    {
                        // AggregateException.GetBaseException gets the first AggregateException with more than one inner exception
                        // OR the first exception that's not an AggregateException.
                        ExceptionDispatchInfo.Capture(t.Exception.GetBaseException()).Throw();
                    }

                    return new LockRelease((AsyncLock)state);
                },
                this,
                cancellationToken,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.asyncSemaphore.Dispose();

                    // This is only disposing the Task...
                    this.lockRelease.Dispose();
                }

                this.disposed = true;
            }
        }

        public struct LockRelease : IDisposable
        {
            readonly AsyncLock asyncLockRelease;
            bool disposed;

            internal LockRelease(AsyncLock release)
            {
                this.asyncLockRelease = release;
                this.disposed = false;
            }

            public void Dispose()
            {
                this.Dispose(true);
            }

            void Dispose(bool disposing)
            {
                if (!this.disposed)
                {
                    if (disposing)
                    {
                        this.asyncLockRelease?.asyncSemaphore.Release();
                    }

                    this.disposed = true;
                }
            }
        }
    }
}
