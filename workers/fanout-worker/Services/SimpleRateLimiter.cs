namespace FanoutWorker.Services;

public sealed class SimpleRateLimiter : IDisposable
{
    private readonly int _permitsPerSecond;
    private readonly SemaphoreSlim _semaphore;
    private readonly Timer _timer;
    private bool _disposed;

    public SimpleRateLimiter(int permitsPerSecond)
    {
        _permitsPerSecond = Math.Max(1, permitsPerSecond);
        _semaphore = new SemaphoreSlim(_permitsPerSecond, _permitsPerSecond);
        _timer = new Timer(Replenish, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public async Task<bool> WaitAsync(CancellationToken cancellationToken)
    {
        if (_semaphore.Wait(0))
        {
            return false;
        }

        await _semaphore.WaitAsync(cancellationToken);
        return true;
    }

    private void Replenish(object? state)
    {
        if (_disposed)
        {
            return;
        }

        var toRelease = _permitsPerSecond - _semaphore.CurrentCount;
        if (toRelease <= 0)
        {
            return;
        }

        _semaphore.Release(toRelease);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Dispose();
        _semaphore.Dispose();
    }
}
