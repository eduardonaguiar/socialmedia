using System.Diagnostics;
using System.Diagnostics.Metrics;

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
    private readonly Histogram<double> _mergeDurationMs;

    public FeedMetrics()
    {
        _feedDurationMs = _meter.CreateHistogram<double>("feed.list.duration_ms");
        _redisDurationMs = _meter.CreateHistogram<double>("feed.redis.duration_ms");
        _redisErrors = _meter.CreateCounter<long>("feed.redis.errors");
        _mergeItems = _meter.CreateCounter<long>("feed_merge_items_total");
        _pullCalls = _meter.CreateCounter<long>("feed_pull_calls_total");
        _partialCelebrityFailures = _meter.CreateCounter<long>("feed_partial_celebrity_pull_failures_total");
        _mergeDurationMs = _meter.CreateHistogram<double>("feed_merge_duration_ms");
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
