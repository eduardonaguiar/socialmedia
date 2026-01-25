using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace GraphService.Metrics;

public sealed class GraphMetrics
{
    private readonly Meter _meter = new("GraphService", "1.0.0");
    private readonly Counter<long> _followCounter;
    private readonly Counter<long> _unfollowCounter;
    private readonly Histogram<double> _listDurationMs;

    public GraphMetrics()
    {
        _followCounter = _meter.CreateCounter<long>("graph.follow.operations");
        _unfollowCounter = _meter.CreateCounter<long>("graph.unfollow.operations");
        _listDurationMs = _meter.CreateHistogram<double>("graph.list.duration_ms");
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
