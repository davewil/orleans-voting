using OrleansVoting;
using OrleansVoting.Contracts.Grains;

namespace OrleansVoting.Data;

public sealed partial class PollService
{
    private class PollWatcherSubscription : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cancellation = new();
        private readonly WeakReference _watcher;
        private Task? _watcherTask;
        private readonly IPollGrain _pollGrain;
        private readonly IPollWatcher _watcherReference;
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        
        public PollWatcherSubscription(IPollWatcher watcher, IPollGrain pollGrain, IPollWatcher watcherReference)
        {
            _pollGrain = pollGrain;
            _watcher = new WeakReference(watcher);
            _watcherReference = watcherReference;
        }

        public async Task InitializeAsync()
        {
            // Establish the subscription before returning to caller
            await _pollGrain.StartWatching(_watcherReference);
            _started.TrySetResult();

            // Start background refresh loop
            _watcherTask = Task.Run(WatchLoop);
        }

        private async Task WatchLoop()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (await timer.WaitForNextTickAsync(_cancellation.Token))
            {
                // When the client disconnects, the .NET garbage collector can clean up the watcher object.
                // When that happens, we will stop watching.
                // Until then, periodically heartbeat the poll grain to let it know we're still watching.
                if (_watcher.IsAlive)
                {
                    try
                    {
                        await _pollGrain.StartWatching(_watcherReference);
                    }
                    catch
                    {
                        // Ignore the exception. We should log it.
                    }
                }
                else
                {
                    // The poll watcher object has been cleaned up, so stop refreshing its subscription.
                    break;
                }
            }

            // Notify the poll grain that we are no longer interested
            _pollGrain.StopWatching(_watcherReference).Ignore();
        }

        public async ValueTask DisposeAsync()
        {
            _cancellation.Cancel();
            try
            {
                if (_watcherTask is not null)
                {
                    await _watcherTask;
                }
            }
            catch
            {
                // TODO: log
            }

            _cancellation.Dispose();
        }

        public Task WaitUntilStartedAsync() => _started.Task;
    }
}