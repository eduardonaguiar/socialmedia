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
    TimeSpan FailureBackoff,
    long CelebrityFollowerThreshold,
    int MaxConcurrentEvents,
    int MaxConcurrentRedisWrites,
    int FollowersPerSecond,
    int RedisWritesPerSecond,
    long KafkaLagThreshold,
    TimeSpan KafkaLagCheckInterval,
    TimeSpan KafkaLagPause,
    TimeSpan KafkaPollTimeout)
{
    public static FanoutOptions FromConfiguration(IConfiguration configuration)
    {
        var hotWindow = int.TryParse(configuration["HOT_WINDOW_MAX_ITEMS"], out var parsedHot) ? parsedHot : 1000;
        var pageSize = int.TryParse(configuration["FOLLOWER_PAGE_SIZE"], out var parsedPage) ? parsedPage : 200;
        var maxPages = int.TryParse(configuration["FOLLOWER_MAX_PAGES"], out var parsedMaxPages) ? parsedMaxPages : 0;
        var ttlDays = int.TryParse(configuration["DEDUP_TTL_DAYS"], out var parsedDays) ? parsedDays : 7;
        var failureBackoffMs = int.TryParse(configuration["FANOUT_FAILURE_BACKOFF_MS"], out var parsedBackoff) ? parsedBackoff : 1000;
        var celebrityThreshold = long.TryParse(configuration["CELEBRITY_FOLLOWER_THRESHOLD"], out var parsedThreshold)
            ? parsedThreshold
            : 100_000;
        var maxConcurrentEvents = int.TryParse(configuration["FANOUT_MAX_CONCURRENT_EVENTS"], out var parsedEvents)
            ? parsedEvents
            : 4;
        var maxConcurrentRedisWrites = int.TryParse(configuration["FANOUT_MAX_CONCURRENT_REDIS_WRITES"], out var parsedWrites)
            ? parsedWrites
            : 16;
        var followersPerSecond = int.TryParse(configuration["FANOUT_FOLLOWERS_PER_SECOND"], out var parsedFollowers)
            ? parsedFollowers
            : 2000;
        var redisWritesPerSecond = int.TryParse(configuration["FANOUT_REDIS_WRITES_PER_SECOND"], out var parsedRedisWrites)
            ? parsedRedisWrites
            : 2000;
        var kafkaLagThreshold = long.TryParse(configuration["FANOUT_KAFKA_LAG_THRESHOLD"], out var parsedLag)
            ? parsedLag
            : 5000;
        var kafkaLagCheckIntervalMs = int.TryParse(configuration["FANOUT_KAFKA_LAG_CHECK_INTERVAL_MS"], out var parsedLagInterval)
            ? parsedLagInterval
            : 5000;
        var kafkaLagPauseMs = int.TryParse(configuration["FANOUT_KAFKA_LAG_PAUSE_MS"], out var parsedLagPause)
            ? parsedLagPause
            : 2000;
        var kafkaPollTimeoutMs = int.TryParse(configuration["KAFKA_POLL_TIMEOUT_MS"], out var parsedPollTimeout)
            ? parsedPollTimeout
            : 1000;

        var graphRetry = RetrySettings.FromConfiguration(configuration, "GRAPH_RETRY", 3);
        var redisRetry = RetrySettings.FromConfiguration(configuration, "REDIS_RETRY", 3);

        return new FanoutOptions(
            hotWindow,
            pageSize,
            maxPages > 0 ? maxPages : null,
            TimeSpan.FromDays(ttlDays),
            graphRetry,
            redisRetry,
            TimeSpan.FromMilliseconds(failureBackoffMs),
            celebrityThreshold,
            Math.Max(1, maxConcurrentEvents),
            Math.Max(1, maxConcurrentRedisWrites),
            Math.Max(1, followersPerSecond),
            Math.Max(1, redisWritesPerSecond),
            Math.Max(1, kafkaLagThreshold),
            TimeSpan.FromMilliseconds(Math.Max(500, kafkaLagCheckIntervalMs)),
            TimeSpan.FromMilliseconds(Math.Max(500, kafkaLagPauseMs)),
            TimeSpan.FromMilliseconds(Math.Max(200, kafkaPollTimeoutMs)));
    }
}
