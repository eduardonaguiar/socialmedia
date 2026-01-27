using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;

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
    public Counter<long> FanoutSkippedCelebrity { get; }
    public Histogram<double> CelebrityClassificationDurationMs { get; }
    public Counter<long> BackpressureApplied { get; }
    public Counter<long> Retries { get; }
    public Counter<long> RetryExhausted { get; }
    private long _kafkaLag;

    public FanoutMetrics()
    {
        EventsConsumed = _meter.CreateCounter<long>("fanout_events_consumed_total");
        EventsProcessed = _meter.CreateCounter<long>("fanout_events_processed_total");
        EventsDeduped = _meter.CreateCounter<long>("fanout_events_deduped_total");
        FollowerPages = _meter.CreateCounter<long>("fanout_follower_pages_total");
        RedisWrites = _meter.CreateCounter<long>("fanout_redis_writes_total");
        Failures = _meter.CreateCounter<long>("fanout_failures_total");
        ProcessingDurationMs = _meter.CreateHistogram<double>("fanout_processing_duration_ms", unit: "ms");
        FanoutSkippedCelebrity = _meter.CreateCounter<long>("fanout_skipped_celebrity_total");
        CelebrityClassificationDurationMs =
            _meter.CreateHistogram<double>("fanout_celebrity_classification_duration_ms", unit: "ms");
        BackpressureApplied = _meter.CreateCounter<long>("fanout_backpressure_applied_total");
        Retries = _meter.CreateCounter<long>("retries_total");
        RetryExhausted = _meter.CreateCounter<long>("retry_exhausted_total");
        _meter.CreateObservableGauge("fanout_kafka_lag", ObserveKafkaLag);
    }

    public IDisposable TrackProcessing() => new StopwatchScope(ProcessingDurationMs);

    public IDisposable TrackCelebrityClassification() => new StopwatchScope(CelebrityClassificationDurationMs);

    public void RecordFailure(string cause)
    {
        Failures.Add(1, new KeyValuePair<string, object?>("cause", cause));
    }

    public void RecordBackpressure(string reason)
    {
        BackpressureApplied.Add(1, new KeyValuePair<string, object?>("reason", reason));
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

    public void UpdateKafkaLag(long lag)
    {
        Interlocked.Exchange(ref _kafkaLag, lag);
    }

    private Measurement<long> ObserveKafkaLag()
    {
        return new Measurement<long>(Interlocked.Read(ref _kafkaLag));
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
