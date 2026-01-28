using FanoutWorker.Metrics;
using FanoutWorker.Options;

namespace FanoutWorker.Services;

public static class RetryHelper
{
    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        RetrySettings settings,
        FanoutMetrics metrics,
        ILogger logger,
        string operationName,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= settings.MaxAttempts; attempt++)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested && attempt < settings.MaxAttempts)
            {
                metrics.RecordRetry("fanout-worker", operationName);
                var delay = GetDelayWithJitter(settings.InitialDelayMs, settings.MaxDelayMs, attempt);
                logger.LogWarning(ex, "Retrying {Operation} (attempt {Attempt}/{MaxAttempts})", operationName, attempt, settings.MaxAttempts);
                await Task.Delay(delay, cancellationToken);
            }
        }

        metrics.RecordRetryExhausted("fanout-worker", operationName);
        throw new InvalidOperationException($"Retry policy exhausted for {operationName}.");
    }

    private static TimeSpan GetDelayWithJitter(int initialDelayMs, int maxDelayMs, int attempt)
    {
        var exponentialDelay = Math.Min(initialDelayMs * Math.Pow(2, attempt - 1), maxDelayMs);
        var jitterMs = Random.Shared.NextDouble() * exponentialDelay;
        return TimeSpan.FromMilliseconds(Math.Max(50, jitterMs));
    }
}
