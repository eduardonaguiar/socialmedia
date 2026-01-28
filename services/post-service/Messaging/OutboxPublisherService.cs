using System.Diagnostics;
using System.Text.Json;
using Confluent.Kafka;
using PostService.Data;
using PostService.Metrics;
using PostService.Services;

namespace PostService.Messaging;

public sealed class OutboxPublisherService : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("PostService.OutboxPublisher");
    private readonly OutboxRepository _outboxRepository;
    private readonly OutboxMetrics _metrics;
    private readonly ILogger<OutboxPublisherService> _logger;
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;
    private readonly KafkaResilience _kafkaResilience;
    private readonly Guid _lockId = Guid.NewGuid();
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _lockTimeout;
    private readonly int _batchSize;

    public OutboxPublisherService(
        OutboxRepository outboxRepository,
        OutboxMetrics metrics,
        ILogger<OutboxPublisherService> logger,
        IProducer<string, string> producer,
        KafkaResilience kafkaResilience,
        string topic,
        TimeSpan pollInterval,
        TimeSpan lockTimeout,
        int batchSize)
    {
        _outboxRepository = outboxRepository;
        _metrics = metrics;
        _logger = logger;
        _producer = producer;
        _kafkaResilience = kafkaResilience;
        _topic = topic;
        _pollInterval = pollInterval;
        _lockTimeout = lockTimeout;
        _batchSize = batchSize;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            IReadOnlyList<OutboxMessage> messages;

            try
            {
                messages = await _outboxRepository.LockPendingAsync(
                    _lockId,
                    _batchSize,
                    _lockTimeout,
                    stoppingToken);
            }
            catch (DependencyUnavailableException ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Postgres unavailable while locking outbox messages");
                await Task.Delay(_pollInterval, stoppingToken);
                continue;
            }

            if (messages.Count == 0)
            {
                await Task.Delay(_pollInterval, stoppingToken);
                continue;
            }

            foreach (var message in messages)
            {
                await PublishAsync(message, stoppingToken);
            }
        }
    }

    private async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("outbox.publish", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination", _topic);
        activity?.SetTag("messaging.message_id", message.OutboxId.ToString());

        try
        {
            var key = ExtractKey(message.PayloadJson);
            var kafkaMessage = new Message<string, string>
            {
                Key = key,
                Value = message.PayloadJson
            };

            await _kafkaResilience.ExecuteAsync(
                async token => await _producer.ProduceAsync(_topic, kafkaMessage, token),
                "kafka.produce",
                cancellationToken);
            await _outboxRepository.MarkPublishedAsync(message.OutboxId, cancellationToken);
            _metrics.PublishSuccess.Add(1);
            _logger.LogInformation("Published outbox message {OutboxId} to topic {Topic}", message.OutboxId, _topic);
        }
        catch (DependencyUnavailableException ex) when (!cancellationToken.IsCancellationRequested)
        {
            await TryRecordFailureAsync(message.OutboxId, ex.Message, cancellationToken);
            _metrics.PublishFailure.Add(1);
            _logger.LogWarning(ex, "Dependency {Dependency} unavailable while publishing outbox message {OutboxId}", ex.Dependency, message.OutboxId);
        }
        catch (Exception ex)
        {
            await TryRecordFailureAsync(message.OutboxId, ex.Message, cancellationToken);
            _metrics.PublishFailure.Add(1);
            _logger.LogWarning(ex, "Failed to publish outbox message {OutboxId}", message.OutboxId);
        }
    }

    private static string ExtractKey(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        if (document.RootElement.TryGetProperty("author_id", out var authorId))
        {
            return authorId.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private async Task TryRecordFailureAsync(Guid outboxId, string message, CancellationToken cancellationToken)
    {
        try
        {
            await _outboxRepository.RecordFailureAsync(outboxId, message, cancellationToken);
        }
        catch (DependencyUnavailableException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Postgres unavailable while recording outbox failure for {OutboxId}", outboxId);
        }
    }
}
