using GraphService.Metrics;
using GraphService.Models;
using Npgsql;
using Microsoft.Extensions.Logging;

namespace GraphService.Services;

public enum CircuitState
{
    Closed = 0,
    Open = 1,
    HalfOpen = 2
}

public sealed class DependencyUnavailableException : Exception
{
    public DependencyUnavailableException(string dependency, Exception? innerException = null)
        : base($"Dependency unavailable: {dependency}.", innerException)
    {
        Dependency = dependency;
    }

    public string Dependency { get; }
}

public sealed class DatabaseResilience
{
    private readonly RetrySettings _retrySettings;
    private readonly CircuitBreakerSettings _breakerSettings;
    private readonly GraphMetrics _metrics;
    private readonly ILogger<DatabaseResilience> _logger;
    private readonly object _lock = new();
    private CircuitState _state = CircuitState.Closed;
    private int _failureCount;
    private DateTimeOffset _openedAt;
    private bool _halfOpenInFlight;

    public DatabaseResilience(
        DatabaseResilienceOptions options,
        GraphMetrics metrics,
        ILogger<DatabaseResilience> logger)
    {
        _retrySettings = options.Retry;
        _breakerSettings = options.CircuitBreaker;
        _metrics = metrics;
        _logger = logger;
        _metrics.UpdateCircuitBreakerState("postgres", _state);
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        if (!TryAcquire())
        {
            throw new DependencyUnavailableException("postgres");
        }

        for (var attempt = 1; attempt <= _retrySettings.MaxAttempts; attempt++)
        {
            try
            {
                var result = await operation(cancellationToken);
                RecordSuccess();
                return result;
            }
            catch (Exception ex) when (IsRetryable(ex, cancellationToken) && attempt < _retrySettings.MaxAttempts)
            {
                _metrics.RecordRetry("graph-service", operationName);
                var delay = GetDelayWithJitter(_retrySettings.InitialDelayMs, _retrySettings.MaxDelayMs, attempt);
                _logger.LogWarning(
                    ex,
                    "Retrying {Operation} in {Delay}ms (attempt {Attempt}/{MaxAttempts})",
                    operationName,
                    delay.TotalMilliseconds,
                    attempt,
                    _retrySettings.MaxAttempts);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex) when (IsRetryable(ex, cancellationToken))
            {
                RecordFailure();
                _metrics.RecordRetryExhausted("graph-service", operationName);
                throw new DependencyUnavailableException("postgres", ex);
            }
        }

        RecordFailure();
        _metrics.RecordRetryExhausted("graph-service", operationName);
        throw new DependencyUnavailableException("postgres");
    }

    private bool TryAcquire()
    {
        lock (_lock)
        {
            if (_state == CircuitState.Open)
            {
                if (DateTimeOffset.UtcNow - _openedAt < _breakerSettings.OpenDuration)
                {
                    return false;
                }

                TransitionTo(CircuitState.HalfOpen);
            }

            if (_state == CircuitState.HalfOpen)
            {
                if (_halfOpenInFlight)
                {
                    return false;
                }

                _halfOpenInFlight = true;
                return true;
            }

            return true;
        }
    }

    private void RecordSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _halfOpenInFlight = false;
            if (_state != CircuitState.Closed)
            {
                TransitionTo(CircuitState.Closed);
            }
        }
    }

    private void RecordFailure()
    {
        lock (_lock)
        {
            _halfOpenInFlight = false;
            if (_state == CircuitState.HalfOpen)
            {
                OpenCircuit();
                return;
            }

            _failureCount++;
            if (_failureCount >= _breakerSettings.FailureThreshold)
            {
                OpenCircuit();
            }
        }
    }

    private void OpenCircuit()
    {
        _failureCount = 0;
        _openedAt = DateTimeOffset.UtcNow;
        TransitionTo(CircuitState.Open);
        _metrics.RecordCircuitBreakerOpened("postgres");
        _logger.LogWarning("Circuit breaker opened for dependency postgres");
    }

    private void TransitionTo(CircuitState state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;
        _metrics.UpdateCircuitBreakerState("postgres", state);
        _logger.LogInformation("Circuit breaker state {State} for dependency postgres", state);
    }

    private static bool IsRetryable(Exception ex, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return ex is NpgsqlException || ex is TimeoutException;
    }

    private static TimeSpan GetDelayWithJitter(int initialDelayMs, int maxDelayMs, int attempt)
    {
        var exponentialDelay = Math.Min(initialDelayMs * Math.Pow(2, attempt - 1), maxDelayMs);
        var jitterMs = Random.Shared.NextDouble() * exponentialDelay;
        return TimeSpan.FromMilliseconds(Math.Max(50, jitterMs));
    }
}
