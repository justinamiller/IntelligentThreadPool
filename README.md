# IntelligentThreadPool
An Intelligent ThreadPool for eliminating over subscription problems on the CLR ThreadPool


#API
````
using (var threadPool = new IntelligentThreadPool.IntelligentThreadPool(
        new IntelligentThreadPoolSettings(numThreads)))
{
    threadPool.QueueUserWorkItem(() => { ... }));
}
````

This creates a IntelligentThreadPool object which allocates a fixed number of threads, each with their own independent task queue.

This IntelligentThreadPool can also be used in combination with a IntelligentThreadPoolTaskScheduler for TPL support, like this:
````
//use 3 threads
var Scheduler = new IntelligentThreadPoolTaskScheduler(new IntelligentThreadPool(new IntelligentThreadPoolSettings(3)));
var Factory = new TaskFactory(Scheduler);

 var task = Factory.StartNew(() =>
{
    //work that'll run on the dedicated thread pool...
});

task.Wait();
````
