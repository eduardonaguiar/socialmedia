using System.Diagnostics;
using FanoutWorker.Metrics;
using FanoutWorker.Options;
using StackExchange.Redis;

namespace FanoutWorker.Services;

public interface IDedupStore
{
    Task<bool> TryClaimAsync(Guid eventId, TimeSpan ttl, CancellationToken cancellationToken);
    Task ReleaseAsync(Guid eventId);
}

public sealed class DedupStore : IDedupStore
{
    private readonly IConnectionMultiplexer _connection;
    private readonly RetrySettings _retry;
    private readonly FanoutMetrics _metrics;
    private readonly ILogger<DedupStore> _logger;

    public DedupStore(IConnectionMultiplexer connection, RetrySettings retry, FanoutMetrics metrics, ILogger<DedupStore> logger)
    {
        _connection = connection;
        _retry = retry;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<bool> TryClaimAsync(Guid eventId, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var key = GetKey(eventId);
        var db = _connection.GetDatabase();

        using var activity = FanoutTelemetry.ActivitySource.StartActivity("redis.SETNX", ActivityKind.Client);
        activity?.SetTag("db.system", "redis");
        activity?.SetTag("db.operation", "SETNX");

        return await RetryHelper.ExecuteAsync(
            async token => await db.StringSetAsync(key, "1", ttl, When.NotExists),
            _retry,
            _metrics,
            _logger,
            "redis.dedup.claim",
            cancellationToken);
    }

    public async Task ReleaseAsync(Guid eventId)
    {
        var key = GetKey(eventId);
        var db = _connection.GetDatabase();
        using var activity = FanoutTelemetry.ActivitySource.StartActivity("redis.DEL", ActivityKind.Client);
        activity?.SetTag("db.system", "redis");
        activity?.SetTag("db.operation", "DEL");
        await db.KeyDeleteAsync(key);
    }

    private static string GetKey(Guid eventId) => $"dedup:post_created:{eventId}";
}
