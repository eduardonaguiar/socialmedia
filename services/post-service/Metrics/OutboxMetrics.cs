using System.Diagnostics.Metrics;

namespace PostService.Metrics;

public sealed class OutboxMetrics
{
    private long _backlog;

    public OutboxMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("PostService.Outbox");
        PublishSuccess = meter.CreateCounter<long>("outbox.publish.success");
        PublishFailure = meter.CreateCounter<long>("outbox.publish.failure");
        meter.CreateObservableGauge("outbox.backlog", ObserveBacklog);
    }

    public Counter<long> PublishSuccess { get; }
    public Counter<long> PublishFailure { get; }

    public void UpdateBacklog(long value)
    {
        Interlocked.Exchange(ref _backlog, value);
    }

    private Measurement<long> ObserveBacklog()
    {
        return new Measurement<long>(Interlocked.Read(ref _backlog));
    }
}
