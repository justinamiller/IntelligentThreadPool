using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentThreadPool
{

    /// <summary>
    /// TaskScheduler for working with a <see cref="IntelligentThreadPool"/> instance
    /// </summary>
    public sealed class IntelligentThreadPoolTaskScheduler : TaskScheduler
    {
        // Indicates whether the current thread is processing work items.
        [ThreadStatic]
        private static bool _currentThreadIsRunningTasks;

        /// <summary>
        /// Number of tasks currently running
        /// </summary>
        private volatile int _parallelWorkers = 0;

        private readonly LinkedList<Task> _tasks = new LinkedList<Task>();

        private readonly IntelligentThreadPool _pool;

        public IntelligentThreadPoolTaskScheduler(IntelligentThreadPool pool)
        {
            _pool = pool;
        }

        protected override void QueueTask(Task task)
        {
            lock (_tasks)
            {
                _tasks.AddLast(task);
            }

            EnsureWorkerRequested();
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            //current thread isn't running any tasks, can't execute inline
            if (!_currentThreadIsRunningTasks) return false;

            //remove the task from the queue if it was previously added
            if (taskWasPreviouslyQueued)
            {
                if (TryDequeue(task))
                {
                    return TryExecuteTask(task);
                }
                else
                {
                    return false;
                }
            }
            return TryExecuteTask(task);
        }

        protected override bool TryDequeue(Task task)
        {
            lock (_tasks)
            {
                return _tasks.Remove(task);
            }
        }

        /// <summary>
        /// Level of concurrency is directly equal to the number of threads
        /// in the <see cref="IntelligentThreadPool"/>.
        /// </summary>
        public override int MaximumConcurrencyLevel
        {
            get { return _pool.Settings.NumThreads; }
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            var lockTaken = false;
            try
            {
                Monitor.TryEnter(_tasks, ref lockTaken);

                //should this be immutable?
                if (lockTaken)
                {
                    return _tasks;
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
            finally
            {
                if (lockTaken) Monitor.Exit(_tasks);
            }
        }

        private void EnsureWorkerRequested()
        {
            var count = _parallelWorkers;
            while (count < _pool.Settings.NumThreads)
            {
                var prev = Interlocked.CompareExchange(ref _parallelWorkers, count + 1, count);
                if (prev == count)
                {
                    RequestWorker();
                    break;
                }
                count = prev;
            }
        }

        private void ReleaseWorker()
        {
            var count = _parallelWorkers;
            while (count > 0)
            {
                var prev = Interlocked.CompareExchange(ref _parallelWorkers, count - 1, count);
                if (prev == count)
                {
                    break;
                }
                count = prev;
            }
        }

        private void RequestWorker()
        {
            _pool.QueueUserWorkItem(() =>
            {
                // this thread is now available for inlining
                _currentThreadIsRunningTasks = true;
                try
                {
                    // Process all available items in the queue. 
                    while (true)
                    {
                        Task item;
                        lock (_tasks)
                        {
                            // done processing
                            if (_tasks.Count == 0)
                            {
                                ReleaseWorker();
                                break;
                            }

                            // Get the next item from the queue
                            item = _tasks.First.Value;
                            _tasks.RemoveFirst();
                        }

                        // Execute the task we pulled out of the queue 
                        TryExecuteTask(item);
                    }
                }
                // We're done processing items on the current thread 
                finally { _currentThreadIsRunningTasks = false; }
            });
        }
    }
}
