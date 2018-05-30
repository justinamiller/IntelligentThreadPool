using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentThreadPool
{
    /// <summary>
    /// The type of threads to use - either foreground or background threads.
    /// </summary>
    public enum ThreadType : byte
    {
        Background = 0,
        Foreground = 1
    }

    /// <summary>
    /// Provides settings for a Intelligent thread pool
    /// </summary>
    public sealed class IntelligentThreadPoolSettings
    {
        /// <summary>
        /// Background threads are the default thread type
        /// </summary>
        public const ThreadType DefaultThreadType = ThreadType.Background;

        public IntelligentThreadPoolSettings(int numThreads,
                                           string name = null,
                                           TimeSpan? deadlockTimeout = null,
                                           ApartmentState apartmentState = ApartmentState.Unknown,
                                           Action<Exception> exceptionHandler = null,
                                           int threadMaxStackSize = 0, int workerQueueMaxSize = 0)
            : this(numThreads, DefaultThreadType, name, deadlockTimeout, apartmentState, exceptionHandler, threadMaxStackSize, workerQueueMaxSize)
        { }

        public IntelligentThreadPoolSettings(int numThreads,
                                           ThreadType threadType,
                                           string name = null,
                                           TimeSpan? deadlockTimeout = null,
                                           ApartmentState apartmentState = ApartmentState.Unknown,
                                           Action<Exception> exceptionHandler = null,
                                           int threadMaxStackSize = 0, int workerQueueMaxSize = 0)
        {
            Name = name ?? ("IntelligentThreadPool-" + Guid.NewGuid());
            ThreadType = threadType;
            NumThreads = numThreads;
            DeadlockTimeout = deadlockTimeout;
            ApartmentState = apartmentState;
            ExceptionHandler = exceptionHandler ?? (ex => { });
            ThreadMaxStackSize = threadMaxStackSize;
            if (workerQueueMaxSize == 0)
            {
                workerQueueMaxSize = numThreads * 100;
            }
            WorkerQueueMaxSize = workerQueueMaxSize;

            if (WorkerQueueMaxSize >= 5000)
            {
                workerQueueMaxSize = 4999;
            }

            if (deadlockTimeout.HasValue && deadlockTimeout.Value.TotalMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException("deadlockTimeout", string.Format("deadlockTimeout must be null or at least 1ms. Was {0}.", deadlockTimeout));
            }

            if (numThreads <= 0)
            {
                throw new ArgumentOutOfRangeException("numThreads", string.Format("numThreads must be at least 1. Was {0}", numThreads));
            }
        }

        /// <summary>
        /// The total number of threads to run in this thread pool.
        /// </summary>
        public int NumThreads { get; private set; }

        /// <summary>
        /// The type of threads to run in this thread pool.
        /// </summary>
        public ThreadType ThreadType { get; private set; }

        /// <summary>
        /// Apartment state for threads to run in this thread pool
        /// </summary>
        public ApartmentState ApartmentState { get; private set; }

        /// <summary>
        /// Interval to check for thread deadlocks.
        /// 
        /// If a thread takes longer than <see cref="DeadlockTimeout"/> it will be aborted
        /// and replaced.
        /// </summary>
        public TimeSpan? DeadlockTimeout { get; private set; }

        public string Name { get; private set; }

        public Action<Exception> ExceptionHandler { get; private set; }

        /// <summary>
        /// Gets the thread stack size, 0 represents the default stack size.
        /// </summary>
        public int ThreadMaxStackSize { get; private set; }

        /// <summary>
        /// Get the size of Worker Queue for scheduling, 0 represents the default queue size.
        /// </summary>
        public int WorkerQueueMaxSize { get; private set; }
    }
}
