using FanoutWorker.Options;

namespace FanoutWorker.Services;

public static class RetryHelper
{
    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        RetrySettings settings,
        ILogger logger,
        string operationName,
        CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromMilliseconds(settings.InitialDelayMs);

        for (var attempt = 1; attempt <= settings.MaxAttempts; attempt++)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (Exception ex) when (attempt < settings.MaxAttempts)
            {
                logger.LogWarning(ex, "Retrying {Operation} (attempt {Attempt}/{MaxAttempts})", operationName, attempt, settings.MaxAttempts);
                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, settings.MaxDelayMs));
            }
        }

        throw new InvalidOperationException($"Retry policy exhausted for {operationName}.");
    }
}
