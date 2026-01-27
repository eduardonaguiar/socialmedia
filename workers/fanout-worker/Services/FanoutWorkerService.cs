using System.Text.Json;
using Confluent.Kafka;
using FanoutWorker.Metrics;
using FanoutWorker.Models;
using FanoutWorker.Options;

namespace FanoutWorker.Services;

public sealed class FanoutWorkerService : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly FanoutProcessor _processor;
    private readonly FanoutMetrics _metrics;
    private readonly FanoutOptions _options;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ILogger<FanoutWorkerService> _logger;
    private readonly string _topic;

    public FanoutWorkerService(
        IConsumer<string, string> consumer,
        FanoutProcessor processor,
        FanoutMetrics metrics,
        FanoutOptions options,
        JsonSerializerOptions serializerOptions,
        KafkaSettings kafkaSettings,
        ILogger<FanoutWorkerService> logger)
    {
        _consumer = consumer;
        _processor = processor;
        _metrics = metrics;
        _options = options;
        _serializerOptions = serializerOptions;
        _logger = logger;
        _topic = kafkaSettings.TopicPostCreated;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(_topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;

                try
                {
                    result = _consumer.Consume(stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    _metrics.RecordFailure("kafka");
                    _logger.LogWarning(ex, "Kafka consume error");
                    continue;
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (result?.Message is null)
                {
                    continue;
                }

                _metrics.EventsConsumed.Add(1);

                PostCreatedEventV1? payload;
                try
                {
                    payload = JsonSerializer.Deserialize<PostCreatedEventV1>(result.Message.Value, _serializerOptions);
                }
                catch (JsonException ex)
                {
                    _metrics.RecordFailure("payload");
                    _logger.LogWarning(ex, "Invalid PostCreated payload");
                    Commit(result);
                    continue;
                }

                if (payload is null)
                {
                    _metrics.RecordFailure("payload");
                    _logger.LogWarning("Missing PostCreated payload");
                    Commit(result);
                    continue;
                }

                var outcome = await _processor.ProcessAsync(payload, stoppingToken);

                if (outcome == ProcessingOutcome.Failed)
                {
                    _consumer.Seek(result.TopicPartitionOffset);
                    await Task.Delay(_options.FailureBackoff, stoppingToken);
                    continue;
                }

                Commit(result);
            }
        }
        finally
        {
            _consumer.Close();
        }
    }

    private void Commit(ConsumeResult<string, string> result)
    {
        try
        {
            _consumer.Commit(result);
        }
        catch (KafkaException ex)
        {
            _metrics.RecordFailure("kafka");
            _logger.LogWarning(ex, "Kafka commit failed");
        }
    }
}
