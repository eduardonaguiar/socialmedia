using FanoutWorker.Metrics;
using FanoutWorker.Models;
using FanoutWorker.Options;
using StackExchange.Redis;

namespace FanoutWorker.Services;

public sealed class FeedWriter
{
    private readonly IConnectionMultiplexer _connection;
    private readonly FanoutMetrics _metrics;
    private readonly RetrySettings _retrySettings;
    private readonly ILogger<FeedWriter> _logger;
    private readonly int _hotWindowMaxItems;

    public FeedWriter(IConnectionMultiplexer connection, FanoutMetrics metrics, FanoutOptions options, ILogger<FeedWriter> logger)
    {
        _connection = connection;
        _metrics = metrics;
        _retrySettings = options.RedisRetry;
        _logger = logger;
        _hotWindowMaxItems = options.HotWindowMaxItems;
    }

    public async Task AddToFeedAsync(string followerId, Guid postId, long createdAtMs, CancellationToken cancellationToken)
    {
        var key = FeedCachePolicy.GetFeedKey(followerId);
        var db = _connection.GetDatabase();

        using var activity = FanoutTelemetry.ActivitySource.StartActivity("redis.feed.write");
        activity?.SetTag("feed.key", key);
        activity?.SetTag("post.id", postId.ToString());

        await RetryHelper.ExecuteAsync(
            async token =>
            {
                await db.SortedSetAddAsync(key, postId.ToString(), createdAtMs);
                _metrics.RedisWrites.Add(1);
                await TrimHotWindowAsync(db, key, token);
                return true;
            },
            _retrySettings,
            _logger,
            "redis.feed.write",
            cancellationToken);
    }

    private async Task TrimHotWindowAsync(IDatabase db, string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var length = await db.SortedSetLengthAsync(key);
        if (!FeedCachePolicy.TryGetTrimRange(length, _hotWindowMaxItems, out var range))
        {
            return;
        }

        _logger.LogDebug("Trimming feed key {Key} to hot window size {MaxItems}", key, _hotWindowMaxItems);
        await db.SortedSetRemoveRangeByRankAsync(key, range.Start, range.Stop);
    }
}
