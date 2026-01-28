using System.Diagnostics;
using FanoutWorker.Metrics;
using FanoutWorker.Models;
using FanoutWorker.Options;
using StackExchange.Redis;
using System.Threading;

namespace FanoutWorker.Services;

public interface IFeedWriter
{
    Task AddToFeedAsync(string followerId, Guid postId, long createdAtMs, CancellationToken cancellationToken);
}

public sealed class FeedWriter : IFeedWriter
{
    private readonly IConnectionMultiplexer _connection;
    private readonly FanoutMetrics _metrics;
    private readonly RetrySettings _retrySettings;
    private readonly ILogger<FeedWriter> _logger;
    private readonly int _hotWindowMaxItems;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly SimpleRateLimiter _rateLimiter;

    public FeedWriter(IConnectionMultiplexer connection, FanoutMetrics metrics, FanoutOptions options, ILogger<FeedWriter> logger)
    {
        _connection = connection;
        _metrics = metrics;
        _retrySettings = options.RedisRetry;
        _logger = logger;
        _hotWindowMaxItems = options.HotWindowMaxItems;
        _concurrencyLimiter = new SemaphoreSlim(options.MaxConcurrentRedisWrites, options.MaxConcurrentRedisWrites);
        _rateLimiter = new SimpleRateLimiter(options.RedisWritesPerSecond);
    }

    public async Task AddToFeedAsync(string followerId, Guid postId, long createdAtMs, CancellationToken cancellationToken)
    {
        var key = FeedCachePolicy.GetFeedKey(followerId);
        var db = _connection.GetDatabase();

        var throttled = await _rateLimiter.WaitAsync(cancellationToken);
        if (throttled)
        {
            _metrics.RecordBackpressure("redis_rate_limit");
        }

        await _concurrencyLimiter.WaitAsync(cancellationToken);
        try
        {
            await RetryHelper.ExecuteAsync(
                async token =>
                {
                    await ExecuteRedisAsync("ZADD", () => db.SortedSetAddAsync(key, postId.ToString(), createdAtMs));
                    _metrics.RedisWrites.Add(1);
                    await TrimHotWindowAsync(db, key, token);
                    return true;
                },
                _retrySettings,
                _metrics,
                _logger,
                "redis.feed.write",
                cancellationToken);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    private async Task TrimHotWindowAsync(IDatabase db, string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var length = await ExecuteRedisAsync("ZCARD", () => db.SortedSetLengthAsync(key));
        if (!FeedCachePolicy.TryGetTrimRange(length, _hotWindowMaxItems, out var range))
        {
            return;
        }

        _logger.LogDebug("Trimming feed key {Key} to hot window size {MaxItems}", key, _hotWindowMaxItems);
        await ExecuteRedisAsync("ZREMRANGEBYRANK", () => db.SortedSetRemoveRangeByRankAsync(key, range.Start, range.Stop));
    }

    private async Task<T> ExecuteRedisAsync<T>(string operation, Func<Task<T>> action)
    {
        using var activity = FanoutTelemetry.ActivitySource.StartActivity($"redis.{operation}", ActivityKind.Client);
        activity?.SetTag("db.system", "redis");
        activity?.SetTag("db.operation", operation);
        return await action();
    }
}
