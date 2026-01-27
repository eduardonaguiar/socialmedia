using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FanoutWorker.Metrics;

public sealed class FanoutMetrics
{
    private readonly Meter _meter = new("FanoutWorker", "0.1.0");
    public Counter<long> EventsConsumed { get; }
    public Counter<long> EventsProcessed { get; }
    public Counter<long> EventsDeduped { get; }
    public Counter<long> FollowerPages { get; }
    public Counter<long> RedisWrites { get; }
    public Counter<long> Failures { get; }
    public Histogram<double> ProcessingDurationMs { get; }

    public FanoutMetrics()
    {
        EventsConsumed = _meter.CreateCounter<long>("fanout_events_consumed_total");
        EventsProcessed = _meter.CreateCounter<long>("fanout_events_processed_total");
        EventsDeduped = _meter.CreateCounter<long>("fanout_events_deduped_total");
        FollowerPages = _meter.CreateCounter<long>("fanout_follower_pages_total");
        RedisWrites = _meter.CreateCounter<long>("fanout_redis_writes_total");
        Failures = _meter.CreateCounter<long>("fanout_failures_total");
        ProcessingDurationMs = _meter.CreateHistogram<double>("fanout_processing_duration_ms", unit: "ms");
    }

    public IDisposable TrackProcessing() => new StopwatchScope(ProcessingDurationMs);

    public void RecordFailure(string cause)
    {
        Failures.Add(1, new KeyValuePair<string, object?>("cause", cause));
    }

    private sealed class StopwatchScope : IDisposable
    {
        private readonly Histogram<double> _histogram;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public StopwatchScope(Histogram<double> histogram)
        {
            _histogram = histogram;
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _histogram.Record(_stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}
