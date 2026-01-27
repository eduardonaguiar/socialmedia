using FeedService.Metrics;
using FeedService.Models;
using Microsoft.Extensions.Logging;

namespace FeedService.Services;

public sealed class RetryPolicy
{
    private readonly FeedMetrics _metrics;
    private readonly ILogger<RetryPolicy> _logger;
    private readonly string _serviceName = "feed-service";

    public RetryPolicy(FeedMetrics metrics, ILogger<RetryPolicy> logger)
    {
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        RetrySettings settings,
        string operationName,
        CancellationToken cancellationToken)
    {
        var baseDelayMs = settings.InitialDelayMs;

        for (var attempt = 1; attempt <= settings.MaxAttempts; attempt++)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (Exception ex) when (IsRetryable(ex, cancellationToken) && attempt < settings.MaxAttempts)
            {
                _metrics.RecordRetry(_serviceName, operationName);
                var delay = GetDelayWithJitter(baseDelayMs, settings.MaxDelayMs, attempt);
                _logger.LogWarning(
                    ex,
                    "Retrying {Operation} in {Delay}ms (attempt {Attempt}/{MaxAttempts})",
                    operationName,
                    delay.TotalMilliseconds,
                    attempt,
                    settings.MaxAttempts);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex) when (IsRetryable(ex, cancellationToken))
            {
                _metrics.RecordRetryExhausted(_serviceName, operationName);
                throw;
            }
        }

        _metrics.RecordRetryExhausted(_serviceName, operationName);
        throw new InvalidOperationException($"Retry policy exhausted for {operationName}.");
    }

    private static bool IsRetryable(Exception ex, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return ex is HttpRequestException || ex is TaskCanceledException;
    }

    private static TimeSpan GetDelayWithJitter(int initialDelayMs, int maxDelayMs, int attempt)
    {
        var exponentialDelay = Math.Min(
            initialDelayMs * Math.Pow(2, attempt - 1),
            maxDelayMs);
        var jitterMs = Random.Shared.NextDouble() * exponentialDelay;
        return TimeSpan.FromMilliseconds(Math.Max(50, jitterMs));
    }
}
