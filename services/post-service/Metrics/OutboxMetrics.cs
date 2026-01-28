using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using PostService.Services;

namespace PostService.Metrics;

public sealed class OutboxMetrics
{
    private long _backlog;
    private readonly ConcurrentDictionary<string, int> _circuitStates = new();

    public OutboxMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("PostService.Outbox");
        PublishSuccess = meter.CreateCounter<long>("outbox.publish.success");
        PublishFailure = meter.CreateCounter<long>("outbox.publish.failure");
        Retries = meter.CreateCounter<long>("retries_total");
        RetryExhausted = meter.CreateCounter<long>("retry_exhausted_total");
        CircuitBreakerOpened = meter.CreateCounter<long>("circuit_breaker_open_total");
        meter.CreateObservableGauge("outbox.backlog", ObserveBacklog);
        meter.CreateObservableGauge<long>(
            "circuit_breaker_state",
            () => _circuitStates.Select(pair =>
                new Measurement<long>(pair.Value, new KeyValuePair<string, object?>("dependency", pair.Key))));
    }

    public Counter<long> PublishSuccess { get; }
    public Counter<long> PublishFailure { get; }
    public Counter<long> Retries { get; }
    public Counter<long> RetryExhausted { get; }
    public Counter<long> CircuitBreakerOpened { get; }

    public void UpdateBacklog(long value)
    {
        Interlocked.Exchange(ref _backlog, value);
    }

    public void RecordRetry(string service, string operation)
    {
        Retries.Add(1,
            new KeyValuePair<string, object?>("service", service),
            new KeyValuePair<string, object?>("operation", operation));
    }

    public void RecordRetryExhausted(string service, string operation)
    {
        RetryExhausted.Add(1,
            new KeyValuePair<string, object?>("service", service),
            new KeyValuePair<string, object?>("operation", operation));
    }

    public void RecordCircuitBreakerOpened(string dependency)
    {
        CircuitBreakerOpened.Add(1, new KeyValuePair<string, object?>("dependency", dependency));
    }

    public void UpdateCircuitBreakerState(string dependency, CircuitState state)
    {
        _circuitStates.AddOrUpdate(dependency, _ => (int)state, (_, _) => (int)state);
    }

    private Measurement<long> ObserveBacklog()
    {
        return new Measurement<long>(Interlocked.Read(ref _backlog));
    }
}
