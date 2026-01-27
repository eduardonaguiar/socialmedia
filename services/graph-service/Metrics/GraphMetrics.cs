using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using GraphService.Services;

namespace GraphService.Metrics;

public sealed class GraphMetrics
{
    private readonly Meter _meter = new("GraphService", "1.0.0");
    private readonly Counter<long> _followCounter;
    private readonly Counter<long> _unfollowCounter;
    private readonly Histogram<double> _listDurationMs;
    private readonly Counter<long> _retries;
    private readonly Counter<long> _retryExhausted;
    private readonly Counter<long> _circuitBreakerOpened;
    private readonly ConcurrentDictionary<string, int> _circuitStates = new();

    public GraphMetrics()
    {
        _followCounter = _meter.CreateCounter<long>("graph.follow.operations");
        _unfollowCounter = _meter.CreateCounter<long>("graph.unfollow.operations");
        _listDurationMs = _meter.CreateHistogram<double>("graph.list.duration_ms");
        _retries = _meter.CreateCounter<long>("retries_total");
        _retryExhausted = _meter.CreateCounter<long>("retry_exhausted_total");
        _circuitBreakerOpened = _meter.CreateCounter<long>("circuit_breaker_open_total");
        _meter.CreateObservableGauge<long>(
            "circuit_breaker_state",
            () => _circuitStates.Select(pair =>
                new Measurement<long>(pair.Value, new KeyValuePair<string, object?>("dependency", pair.Key))));
    }

    public void RecordFollow(bool created)
    {
        _followCounter.Add(1, new KeyValuePair<string, object?>("created", created));
    }

    public void RecordUnfollow(bool removed)
    {
        _unfollowCounter.Add(1, new KeyValuePair<string, object?>("removed", removed));
    }

    public IDisposable TrackList(string listType)
    {
        var stopwatch = Stopwatch.StartNew();
        return new ListTimer(stopwatch, _listDurationMs, listType);
    }

    public void RecordRetry(string service, string operation)
    {
        _retries.Add(1,
            new KeyValuePair<string, object?>("service", service),
            new KeyValuePair<string, object?>("operation", operation));
    }

    public void RecordRetryExhausted(string service, string operation)
    {
        _retryExhausted.Add(1,
            new KeyValuePair<string, object?>("service", service),
            new KeyValuePair<string, object?>("operation", operation));
    }

    public void RecordCircuitBreakerOpened(string dependency)
    {
        _circuitBreakerOpened.Add(1, new KeyValuePair<string, object?>("dependency", dependency));
    }

    public void UpdateCircuitBreakerState(string dependency, CircuitState state)
    {
        _circuitStates.AddOrUpdate(dependency, _ => (int)state, (_, _) => (int)state);
    }

    private sealed class ListTimer : IDisposable
    {
        private readonly Stopwatch _stopwatch;
        private readonly Histogram<double> _histogram;
        private readonly string _listType;
        private bool _disposed;

        public ListTimer(Stopwatch stopwatch, Histogram<double> histogram, string listType)
        {
            _stopwatch = stopwatch;
            _histogram = histogram;
            _listType = listType;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stopwatch.Stop();
            _histogram.Record(_stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("list_type", _listType));
        }
    }
}
