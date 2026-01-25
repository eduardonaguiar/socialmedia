using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FeedService.Metrics;

public sealed class FeedMetrics
{
    private readonly Meter _meter = new("FeedService", "1.0.0");
    private readonly Histogram<double> _feedDurationMs;
    private readonly Histogram<double> _redisDurationMs;
    private readonly Counter<long> _redisErrors;

    public FeedMetrics()
    {
        _feedDurationMs = _meter.CreateHistogram<double>("feed.list.duration_ms");
        _redisDurationMs = _meter.CreateHistogram<double>("feed.redis.duration_ms");
        _redisErrors = _meter.CreateCounter<long>("feed.redis.errors");
    }

    public IDisposable TrackFeedList() => new TimerScope(_feedDurationMs, "feed_list");

    public IDisposable TrackRedisOperation(string operation) => new TimerScope(_redisDurationMs, operation);

    public void RecordRedisError(string operation)
    {
        _redisErrors.Add(1, new KeyValuePair<string, object?>("operation", operation));
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
