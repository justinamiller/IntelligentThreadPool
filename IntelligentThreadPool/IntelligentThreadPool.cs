using IntelligentThreadPool.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentThreadPool
{

    /// <summary>
    /// An instanced, Intelligent thread pool.
    /// </summary>
    public sealed class IntelligentThreadPool : IDisposable
    {
        public IntelligentThreadPool(IntelligentThreadPoolSettings settings)
        {
            _workQueue = new ThreadPoolWorkQueue(settings.WorkerQueueMaxSize);
            Settings = settings;
            _workers = Enumerable.Range(1, settings.NumThreads).Select(workerId => new PoolWorker(this, workerId)).ToArray();

            // Note:
            // The IntelligentThreadPoolSupervisor was removed because aborting thread could lead to unexpected behavior
            // If a new implementation is done, it should spawn a new thread when a worker is not making progress and
            // try to keep {settings.NumThreads} active threads.
        }

        public IntelligentThreadPoolSettings Settings { get; private set; }

        private readonly ThreadPoolWorkQueue _workQueue;
        private readonly PoolWorker[] _workers;

        internal ThreadPoolWorkQueue WorkQueue
        {
            get
            {
                return _workQueue;
            }
        }

        public bool QueueUserWorkItem(Action work)
        {
            if (work == null)
            {
                throw new ArgumentNullException("work");
            }

            return _workQueue.TryAdd(work);
        }

        public void Dispose()
        {
            _workQueue.CompleteAdding();
        }

        public void WaitForThreadsExit()
        {
            WaitForThreadsExit(Timeout.InfiniteTimeSpan);
        }

        public void WaitForThreadsExit(TimeSpan timeout)
        {
            _workQueue.CompleteAdding();
            Task.WaitAll(_workers.Select(worker => worker.ThreadExit).ToArray(), timeout);
        }


        #region WorkQueue implementation



        #endregion

        #region UnfairSemaphore implementation



        #endregion
    }
}
