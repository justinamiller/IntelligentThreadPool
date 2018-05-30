using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IntelligentThreadPool.Core
{
    class PoolWorker
    {
        private readonly IntelligentThreadPool _pool;

        private readonly TaskCompletionSource<object> _threadExit;

        public Task ThreadExit
        {
            get { return _threadExit.Task; }
        }

        public PoolWorker(IntelligentThreadPool pool, int workerId)
        {
            _pool = pool;
            _threadExit = new TaskCompletionSource<object>();

            var thread = new Thread(RunThread, pool.Settings.ThreadMaxStackSize)
            {
                IsBackground = pool.Settings.ThreadType == ThreadType.Background
            };

            if (pool.Settings.Name != null)
            {
                thread.Name = string.Format("{0}_{1}", pool.Settings.Name, workerId);
            }

            if (pool.Settings.ApartmentState != ApartmentState.Unknown)
            {
                thread.SetApartmentState(pool.Settings.ApartmentState);
            }


            thread.Start();
        }

        private void RunThread()
        {
            try
            {
                foreach (var action in _pool.WorkQueue.GetConsumingEnumerable())
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        _pool.Settings.ExceptionHandler(ex);
                    }
                }
            }
            finally
            {
                _threadExit.TrySetResult(null);
            }
        }
    }
}
