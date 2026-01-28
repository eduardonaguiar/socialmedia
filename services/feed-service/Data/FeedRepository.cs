using System.Diagnostics;
using FeedService.Metrics;
using FeedService.Models;
using StackExchange.Redis;

namespace FeedService.Data;

public sealed class FeedRepository
{
    private readonly IConnectionMultiplexer _connection;
    private readonly FeedMetrics _metrics;
    private readonly ILogger<FeedRepository> _logger;

    public FeedRepository(IConnectionMultiplexer connection, FeedMetrics metrics, ILogger<FeedRepository> logger)
    {
        _connection = connection;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FeedEntry>> GetFeedPageAsync(
        string userId,
        CursorPayload? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = FeedCachePolicy.GetFeedKey(userId);
        var db = _connection.GetDatabase();

        if (cursor is null)
        {
            var entries = await ExecuteWithMetricsAsync(
                "zrevrangebyscore",
                () => db.SortedSetRangeByScoreWithScoresAsync(
                    key,
                    double.PositiveInfinity,
                    double.NegativeInfinity,
                    Exclude.None,
                    Order.Descending,
                    0,
                    limit),
                activity => activity?.SetTag("feed.limit", limit));

            return MapEntries(entries, limit);
        }

        var results = new List<FeedEntry>(limit);

        var sameScoreEntries = await ExecuteWithMetricsAsync(
            "zrevrangebyscore_same_score",
            () => db.SortedSetRangeByScoreWithScoresAsync(
                key,
                cursor.Score,
                cursor.Score,
                Exclude.None,
                Order.Descending),
            activity => activity?.SetTag("feed.limit", limit));

        foreach (var entry in sameScoreEntries)
        {
            if (results.Count >= limit)
            {
                break;
            }

            if (!entry.Element.HasValue)
            {
                continue;
            }

            var member = entry.Element.ToString();
            if (string.CompareOrdinal(member, cursor.Member) < 0)
            {
                results.Add(new FeedEntry(member, Convert.ToInt64(entry.Score)));
            }
        }

        if (results.Count >= limit)
        {
            return results;
        }

        var remaining = limit - results.Count;
        var belowEntries = await ExecuteWithMetricsAsync(
            "zrevrangebyscore_below",
            () => db.SortedSetRangeByScoreWithScoresAsync(
                key,
                cursor.Score,
                double.NegativeInfinity,
                Exclude.Start,
                Order.Descending,
                0,
                remaining),
            activity => activity?.SetTag("feed.limit", limit));

        results.AddRange(MapEntries(belowEntries, remaining));
        return results;
    }

    public async Task TrimHotWindowAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = FeedCachePolicy.GetFeedKey(userId);
        var db = _connection.GetDatabase();

        var length = await ExecuteWithMetricsAsync("zcard", () => db.SortedSetLengthAsync(key));
        if (!FeedCachePolicy.TryGetTrimRange(length, FeedCachePolicy.HotWindowMaxItems, out var range))
        {
            return;
        }

        await ExecuteWithMetricsAsync(
            "zremrangebyrank",
            () => db.SortedSetRemoveRangeByRankAsync(key, range.Start, range.Stop));
    }

    private async Task<T> ExecuteWithMetricsAsync<T>(string operation, Func<Task<T>> action, Action<Activity?>? enrich = null)
    {
        using var timer = _metrics.TrackRedisOperation(operation);
        using var activity = FeedTelemetry.ActivitySource.StartActivity($"redis.{operation}", ActivityKind.Client);
        activity?.SetTag("db.system", "redis");
        activity?.SetTag("db.operation", operation);
        enrich?.Invoke(activity);

        try
        {
            return await action();
        }
        catch (RedisException ex)
        {
            _metrics.RecordRedisError(operation);
            _logger.LogWarning(ex, "Redis operation {Operation} failed", operation);
            throw;
        }
    }

    private static List<FeedEntry> MapEntries(SortedSetEntry[] entries, int take)
    {
        var results = new List<FeedEntry>(Math.Min(entries.Length, take));

        foreach (var entry in entries)
        {
            if (results.Count >= take)
            {
                break;
            }

            if (!entry.Element.HasValue)
            {
                continue;
            }

            results.Add(new FeedEntry(entry.Element.ToString(), Convert.ToInt64(entry.Score)));
        }

        return results;
    }
}
