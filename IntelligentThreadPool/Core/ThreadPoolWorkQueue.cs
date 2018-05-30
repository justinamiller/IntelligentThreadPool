using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentThreadPool.Core
{
    class ThreadPoolWorkQueue
    {
        private static readonly int ProcessorCount = Environment.ProcessorCount;
        private const int CompletedState = 1;

        private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();
        private readonly UnfairSemaphore _semaphore = new UnfairSemaphore();
        private int _outstandingRequests;
        private int _isAddingCompleted;
        private readonly int _maxQueueSize = 0;
        private int _queueSize = 0;
        private readonly AutoResetEvent _onFullResetEvent = new AutoResetEvent(false);

        public int QueueSize { get { return Volatile.Read(ref _queueSize); } }

        public bool IsAddingCompleted
        {
            get { return Volatile.Read(ref _isAddingCompleted) == CompletedState; }
        }

        public ThreadPoolWorkQueue(int maxQueueSize)
        {
            _maxQueueSize = maxQueueSize;
        }

        public bool TryAdd(Action work)
        {
            // If TryAdd returns true, it's garanteed the work item will be executed.
            // If it returns false, it's also garanteed the work item won't be executed.

            if (IsAddingCompleted)
            {
                return false;
            }

            _queue.Enqueue(work);

            EnsureQueueSizeIsNotFull();

            EnsureThreadRequested();

            return true;
        }

        private void EnsureQueueSizeIsNotFull()
        {
            Interlocked.Increment(ref _queueSize);
            if (_queueSize >= _maxQueueSize)
            {
                //queue is full block futher request
                _onFullResetEvent.Reset();
                do
                {
                    if (_onFullResetEvent.WaitOne(50))
                    {
                        //queue open continue.
                        return;
                    }
                }
                while (_queueSize >= _maxQueueSize);
                _onFullResetEvent.Set();
            }
        }


        public IEnumerable<Action> GetConsumingEnumerable()
        {
            while (true)
            {
                Action work = null;
                if (_queue.TryDequeue(out work))
                {
                    Interlocked.Decrement(ref _queueSize);
                    _onFullResetEvent.Set();
                    yield return work;
                }
                else if (IsAddingCompleted)
                {
                    while (_queue.TryDequeue(out work))
                    {
                        Interlocked.Decrement(ref _queueSize);
                        _onFullResetEvent.Set();
                        yield return work;
                    }
                    break;
                }
                else
                {
                    _semaphore.Wait();
                    MarkThreadRequestSatisfied();
                }
            }
        }

        public void CompleteAdding()
        {
            int previousCompleted = Interlocked.Exchange(ref _isAddingCompleted, CompletedState);

            if (previousCompleted == CompletedState)
            {
                return;
            }

            // When CompleteAdding() is called, we fill up the _outstandingRequests and the semaphore
            // This will ensure that all threads will unblock and try to execute the remaining item in
            // the queue. When IsAddingCompleted is set, all threads will exit once the queue is empty.

            while (true)
            {
                int count = Volatile.Read(ref _outstandingRequests);
                int countToRelease = UnfairSemaphore.MaxWorker - count;

                int prev = Interlocked.CompareExchange(ref _outstandingRequests, UnfairSemaphore.MaxWorker, count);

                if (prev == count)
                {
                    _semaphore.Release((short)countToRelease);
                    break;
                }
            }
        }

        private void EnsureThreadRequested()
        {
            // There is a double counter here (_outstandingRequest and _semaphore)
            // Unfair semaphore does not support value bigger than short.MaxValue,
            // tring to Release more than short.MaxValue could fail miserably.

            // The _outstandingRequest counter ensure that we only request a
            // maximum of {ProcessorCount} to the semaphore.

            // It's also more efficient to have two counter, _outstandingRequests is
            // more lightweight than the semaphore.

            // This trick is borrowed from the .Net ThreadPool
            // https://github.com/dotnet/coreclr/blob/bc146608854d1db9cdbcc0b08029a87754e12b49/src/mscorlib/src/System/Threading/ThreadPool.cs#L568

            int count = Volatile.Read(ref _outstandingRequests);
            while (count < ProcessorCount)
            {
                int prev = Interlocked.CompareExchange(ref _outstandingRequests, count + 1, count);
                if (prev == count)
                {
                    _semaphore.Release();
                    break;
                }
                count = prev;
            }
        }

        private void MarkThreadRequestSatisfied()
        {
            int count = Volatile.Read(ref _outstandingRequests);
            while (count > 0)
            {
                int prev = Interlocked.CompareExchange(ref _outstandingRequests, count - 1, count);
                if (prev == count)
                {
                    break;
                }
                count = prev;
            }
        }
    }
}
