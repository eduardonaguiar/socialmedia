namespace PostService.Models;

public sealed record RetrySettings(int MaxAttempts, int InitialDelayMs, int MaxDelayMs)
{
    public static RetrySettings FromConfiguration(IConfiguration configuration, string prefix, int defaultMaxAttempts)
    {
        var maxAttempts = int.TryParse(configuration[$"{prefix}_MAX_ATTEMPTS"], out var parsedMax)
            ? parsedMax
            : defaultMaxAttempts;
        var initialDelay = int.TryParse(configuration[$"{prefix}_INITIAL_DELAY_MS"], out var parsedDelay)
            ? parsedDelay
            : 200;
        var maxDelay = int.TryParse(configuration[$"{prefix}_MAX_DELAY_MS"], out var parsedMaxDelay)
            ? parsedMaxDelay
            : 2000;

        return new RetrySettings(Math.Max(1, maxAttempts), Math.Max(50, initialDelay), Math.Max(100, maxDelay));
    }
}

public sealed record CircuitBreakerSettings(int FailureThreshold, TimeSpan OpenDuration)
{
    public static CircuitBreakerSettings FromConfiguration(IConfiguration configuration, string prefix, int defaultThreshold)
    {
        var threshold = int.TryParse(configuration[$"{prefix}_FAILURE_THRESHOLD"], out var parsedThreshold)
            ? parsedThreshold
            : defaultThreshold;
        var openSeconds = int.TryParse(configuration[$"{prefix}_OPEN_SECONDS"], out var parsedOpen)
            ? parsedOpen
            : 15;

        return new CircuitBreakerSettings(Math.Max(1, threshold), TimeSpan.FromSeconds(Math.Max(1, openSeconds)));
    }
}

public sealed record DatabaseResilienceOptions(
    RetrySettings Retry,
    CircuitBreakerSettings CircuitBreaker)
{
    public static DatabaseResilienceOptions FromConfiguration(IConfiguration configuration)
    {
        var retry = RetrySettings.FromConfiguration(configuration, "DB_RETRY", 3);
        var breaker = CircuitBreakerSettings.FromConfiguration(configuration, "DB_CIRCUIT_BREAKER", 4);
        return new DatabaseResilienceOptions(retry, breaker);
    }
}

public sealed record KafkaResilienceOptions(
    RetrySettings Retry,
    CircuitBreakerSettings CircuitBreaker)
{
    public static KafkaResilienceOptions FromConfiguration(IConfiguration configuration)
    {
        var retry = RetrySettings.FromConfiguration(configuration, "KAFKA_RETRY", 3);
        var breaker = CircuitBreakerSettings.FromConfiguration(configuration, "KAFKA_CIRCUIT_BREAKER", 4);
        return new KafkaResilienceOptions(retry, breaker);
    }
}
