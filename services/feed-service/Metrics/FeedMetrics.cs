using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using FeedService.Services;

namespace FeedService.Metrics;

public sealed class FeedMetrics
{
    private readonly Meter _meter = new("FeedService", "1.0.0");
    private readonly Histogram<double> _feedDurationMs;
    private readonly Histogram<double> _redisDurationMs;
    private readonly Counter<long> _redisErrors;
    private readonly Counter<long> _mergeItems;
    private readonly Counter<long> _pullCalls;
    private readonly Counter<long> _partialCelebrityFailures;
    private readonly Counter<long> _partialResponses;
    private readonly Counter<long> _retries;
    private readonly Counter<long> _retryExhausted;
    private readonly Counter<long> _circuitBreakerOpened;
    private readonly ConcurrentDictionary<string, int> _circuitStates = new();
    private readonly Histogram<double> _mergeDurationMs;

    public FeedMetrics()
    {
        _feedDurationMs = _meter.CreateHistogram<double>("feed.list.duration_ms");
        _redisDurationMs = _meter.CreateHistogram<double>("feed.redis.duration_ms");
        _redisErrors = _meter.CreateCounter<long>("feed.redis.errors");
        _mergeItems = _meter.CreateCounter<long>("feed_merge_items_total");
        _pullCalls = _meter.CreateCounter<long>("feed_pull_calls_total");
        _partialCelebrityFailures = _meter.CreateCounter<long>("feed_partial_celebrity_pull_failures_total");
        _partialResponses = _meter.CreateCounter<long>("feed_partial_responses_total");
        _retries = _meter.CreateCounter<long>("retries_total");
        _retryExhausted = _meter.CreateCounter<long>("retry_exhausted_total");
        _mergeDurationMs = _meter.CreateHistogram<double>("feed_merge_duration_ms");
        _circuitBreakerOpened = _meter.CreateCounter<long>("circuit_breaker_open_total");

        _meter.CreateObservableGauge<long>(
            "circuit_breaker_state",
            () => _circuitStates.Select(pair =>
                new Measurement<long>(pair.Value, new KeyValuePair<string, object?>("dependency", pair.Key))));
    }

    public IDisposable TrackFeedList() => new TimerScope(_feedDurationMs, "feed_list");

    public IDisposable TrackRedisOperation(string operation) => new TimerScope(_redisDurationMs, operation);

    public IDisposable TrackMerge() => new TimerScope(_mergeDurationMs, "merge");

    public void RecordRedisError(string operation)
    {
        _redisErrors.Add(1, new KeyValuePair<string, object?>("operation", operation));
    }

    public void RecordMergeItems(string source, int count)
    {
        if (count <= 0)
        {
            return;
        }

        _mergeItems.Add(count, new KeyValuePair<string, object?>("source", source));
    }

    public void RecordPullCall(string target, bool success)
    {
        _pullCalls.Add(1,
            new KeyValuePair<string, object?>("target", target),
            new KeyValuePair<string, object?>("success", success));
    }

    public void RecordCelebrityPullFailure(string cause)
    {
        _partialCelebrityFailures.Add(1, new KeyValuePair<string, object?>("cause", cause));
    }

    public void RecordPartialResponse(string reason)
    {
        _partialResponses.Add(1, new KeyValuePair<string, object?>("reason", reason));
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

    private sealed class TimerScope : IDisposable
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly Histogram<double> _histogram;
        private readonly string _operation;
        private bool _disposed;

        public TimerScope(Histogram<double> histogram, string operation)
        {
            _histogram = histogram;
            _operation = operation;
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
                new KeyValuePair<string, object?>("operation", _operation));
        }
    }
}
