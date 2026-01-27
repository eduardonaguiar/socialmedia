using Confluent.Kafka;
using PostService.Metrics;
using PostService.Models;
using Microsoft.Extensions.Logging;

namespace PostService.Services;

public sealed class KafkaResilience
{
    private readonly RetrySettings _retrySettings;
    private readonly CircuitBreakerSettings _breakerSettings;
    private readonly OutboxMetrics _metrics;
    private readonly ILogger<KafkaResilience> _logger;
    private readonly object _lock = new();
    private CircuitState _state = CircuitState.Closed;
    private int _failureCount;
    private DateTimeOffset _openedAt;
    private bool _halfOpenInFlight;

    public KafkaResilience(
        KafkaResilienceOptions options,
        OutboxMetrics metrics,
        ILogger<KafkaResilience> logger)
    {
        _retrySettings = options.Retry;
        _breakerSettings = options.CircuitBreaker;
        _metrics = metrics;
        _logger = logger;
        _metrics.UpdateCircuitBreakerState("kafka", _state);
    }

    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        if (!TryAcquire())
        {
            throw new DependencyUnavailableException("kafka");
        }

        for (var attempt = 1; attempt <= _retrySettings.MaxAttempts; attempt++)
        {
            try
            {
                await operation(cancellationToken);
                RecordSuccess();
                return;
            }
            catch (Exception ex) when (IsRetryable(ex, cancellationToken) && attempt < _retrySettings.MaxAttempts)
            {
                _metrics.RecordRetry("post-service", operationName);
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
                _metrics.RecordRetryExhausted("post-service", operationName);
                throw new DependencyUnavailableException("kafka", ex);
            }
        }

        RecordFailure();
        _metrics.RecordRetryExhausted("post-service", operationName);
        throw new DependencyUnavailableException("kafka");
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
        _metrics.RecordCircuitBreakerOpened("kafka");
        _logger.LogWarning("Circuit breaker opened for dependency kafka");
    }

    private void TransitionTo(CircuitState state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;
        _metrics.UpdateCircuitBreakerState("kafka", state);
        _logger.LogInformation("Circuit breaker state {State} for dependency kafka", state);
    }

    private static bool IsRetryable(Exception ex, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return ex is TimeoutException || ex is OperationCanceledException || ex is KafkaException;
    }

    private static TimeSpan GetDelayWithJitter(int initialDelayMs, int maxDelayMs, int attempt)
    {
        var exponentialDelay = Math.Min(initialDelayMs * Math.Pow(2, attempt - 1), maxDelayMs);
        var jitterMs = Random.Shared.NextDouble() * exponentialDelay;
        return TimeSpan.FromMilliseconds(Math.Max(50, jitterMs));
    }
}
