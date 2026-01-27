namespace FanoutWorker.Options;

public sealed record RetrySettings(int MaxAttempts, int InitialDelayMs, int MaxDelayMs)
{
    public static RetrySettings FromConfiguration(IConfiguration configuration, string prefix, int defaultMaxAttempts)
    {
        var maxAttempts = int.TryParse(configuration[$"{prefix}_MAX_ATTEMPTS"], out var parsedMax) ? parsedMax : defaultMaxAttempts;
        var initialDelay = int.TryParse(configuration[$"{prefix}_INITIAL_DELAY_MS"], out var parsedDelay) ? parsedDelay : 200;
        var maxDelay = int.TryParse(configuration[$"{prefix}_MAX_DELAY_MS"], out var parsedMaxDelay) ? parsedMaxDelay : 2000;

        return new RetrySettings(maxAttempts, initialDelay, maxDelay);
    }
}

public sealed record FanoutOptions(
    int HotWindowMaxItems,
    int FollowerPageSize,
    int? MaxFollowerPages,
    TimeSpan DedupTtl,
    RetrySettings GraphRetry,
    RetrySettings RedisRetry,
    TimeSpan FailureBackoff)
{
    public static FanoutOptions FromConfiguration(IConfiguration configuration)
    {
        var hotWindow = int.TryParse(configuration["HOT_WINDOW_MAX_ITEMS"], out var parsedHot) ? parsedHot : 1000;
        var pageSize = int.TryParse(configuration["FOLLOWER_PAGE_SIZE"], out var parsedPage) ? parsedPage : 200;
        var maxPages = int.TryParse(configuration["FOLLOWER_MAX_PAGES"], out var parsedMaxPages) ? parsedMaxPages : 0;
        var ttlDays = int.TryParse(configuration["DEDUP_TTL_DAYS"], out var parsedDays) ? parsedDays : 7;
        var failureBackoffMs = int.TryParse(configuration["FANOUT_FAILURE_BACKOFF_MS"], out var parsedBackoff) ? parsedBackoff : 1000;

        var graphRetry = RetrySettings.FromConfiguration(configuration, "GRAPH_RETRY", 3);
        var redisRetry = RetrySettings.FromConfiguration(configuration, "REDIS_RETRY", 3);

        return new FanoutOptions(
            hotWindow,
            pageSize,
            maxPages > 0 ? maxPages : null,
            TimeSpan.FromDays(ttlDays),
            graphRetry,
            redisRetry,
            TimeSpan.FromMilliseconds(failureBackoffMs));
    }
}
