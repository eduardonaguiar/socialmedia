namespace FeedService.Models;

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

public sealed record FeedResilienceOptions(
    RetrySettings GraphRetry,
    RetrySettings PostRetry,
    CircuitBreakerSettings GraphCircuitBreaker,
    CircuitBreakerSettings PostCircuitBreaker,
    TimeSpan GraphTimeout,
    TimeSpan PostTimeout)
{
    public static FeedResilienceOptions FromConfiguration(IConfiguration configuration)
    {
        var graphRetry = RetrySettings.FromConfiguration(configuration, "GRAPH_RETRY", 3);
        var postRetry = RetrySettings.FromConfiguration(configuration, "POST_RETRY", 3);
        var graphCircuit = CircuitBreakerSettings.FromConfiguration(configuration, "GRAPH_CIRCUIT_BREAKER", 4);
        var postCircuit = CircuitBreakerSettings.FromConfiguration(configuration, "POST_CIRCUIT_BREAKER", 4);
        var graphTimeoutMs = int.TryParse(configuration["GRAPH_HTTP_TIMEOUT_MS"], out var parsedGraphTimeout)
            ? parsedGraphTimeout
            : 1500;
        var postTimeoutMs = int.TryParse(configuration["POST_HTTP_TIMEOUT_MS"], out var parsedPostTimeout)
            ? parsedPostTimeout
            : 1500;

        return new FeedResilienceOptions(
            graphRetry,
            postRetry,
            graphCircuit,
            postCircuit,
            TimeSpan.FromMilliseconds(Math.Max(100, graphTimeoutMs)),
            TimeSpan.FromMilliseconds(Math.Max(100, postTimeoutMs)));
    }
}
