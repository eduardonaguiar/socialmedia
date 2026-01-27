using FeedService.Metrics;
using FeedService.Models;
using Microsoft.Extensions.Logging;

namespace FeedService.Services;

public enum CircuitState
{
    Closed = 0,
    Open = 1,
    HalfOpen = 2
}

public sealed class CircuitBreaker
{
    private readonly CircuitBreakerSettings _settings;
    private readonly FeedMetrics _metrics;
    private readonly string _dependency;
    private readonly ILogger _logger;
    private readonly object _lock = new();
    private CircuitState _state = CircuitState.Closed;
    private int _failureCount;
    private DateTimeOffset _openedAt;
    private bool _halfOpenInFlight;

    public CircuitBreaker(CircuitBreakerSettings settings, FeedMetrics metrics, ILogger logger, string dependency)
    {
        _settings = settings;
        _metrics = metrics;
        _logger = logger;
        _dependency = dependency;
        _metrics.UpdateCircuitBreakerState(_dependency, _state);
    }

    public bool TryAcquire()
    {
        lock (_lock)
        {
            if (_state == CircuitState.Open)
            {
                if (DateTimeOffset.UtcNow - _openedAt < _settings.OpenDuration)
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

    public void RecordSuccess()
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

    public void RecordFailure()
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
            if (_failureCount >= _settings.FailureThreshold)
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
        _metrics.RecordCircuitBreakerOpened(_dependency);
        _logger.LogWarning("Circuit breaker opened for dependency {Dependency}", _dependency);
    }

    private void TransitionTo(CircuitState state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;
        _metrics.UpdateCircuitBreakerState(_dependency, state);
        _logger.LogInformation("Circuit breaker state {State} for dependency {Dependency}", state, _dependency);
    }
}
