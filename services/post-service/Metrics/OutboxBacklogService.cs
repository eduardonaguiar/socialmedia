using PostService.Data;

namespace PostService.Metrics;

public sealed class OutboxBacklogService : BackgroundService
{
    private readonly OutboxRepository _outboxRepository;
    private readonly OutboxMetrics _metrics;
    private readonly ILogger<OutboxBacklogService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(10);

    public OutboxBacklogService(
        OutboxRepository outboxRepository,
        OutboxMetrics metrics,
        ILogger<OutboxBacklogService> logger)
    {
        _outboxRepository = outboxRepository;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var backlog = await _outboxRepository.GetBacklogAsync(stoppingToken);
                _metrics.UpdateBacklog(backlog);
            }
            catch (Exception ex)
            {
                _metrics.UpdateBacklog(0);
                _logger.LogWarning(ex, "Failed to read outbox backlog.");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
