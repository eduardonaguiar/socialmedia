using System.Text.Json;
using Confluent.Kafka;
using FanoutWorker.Metrics;
using FanoutWorker.Models;
using FanoutWorker.Options;
using System.Threading.Channels;
using System.Threading;

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
    private readonly SemaphoreSlim _eventLimiter;
    private readonly Channel<ProcessingCompletion> _completions;
    private DateTimeOffset _lastLagCheck = DateTimeOffset.MinValue;
    private DateTimeOffset? _pauseUntil;
    private bool _paused;

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
        _eventLimiter = new SemaphoreSlim(options.MaxConcurrentEvents, options.MaxConcurrentEvents);
        _completions = Channel.CreateUnbounded<ProcessingCompletion>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(_topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await DrainCompletionsAsync(stoppingToken);
                await ApplyLagBackpressureAsync(stoppingToken);

                if (await RespectPauseAsync(stoppingToken))
                {
                    continue;
                }

                ConsumeResult<string, string>? result = null;

                try
                {
                    result = _consumer.Consume(_options.KafkaPollTimeout);
                }
                catch (ConsumeException ex)
                {
                    _metrics.RecordFailure("kafka");
                    _logger.LogWarning(ex, "Kafka consume error");
                    _metrics.RecordBackpressure("kafka_broker");
                    PauseConsumption();
                    _pauseUntil = DateTimeOffset.UtcNow.Add(_options.KafkaLagPause);
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

                if (!_eventLimiter.Wait(0))
                {
                    _metrics.RecordBackpressure("event_concurrency");
                    await _eventLimiter.WaitAsync(stoppingToken);
                }

                _ = ProcessMessageAsync(result, payload, stoppingToken);
            }
        }
        finally
        {
            _consumer.Close();
        }
    }

    private async Task ProcessMessageAsync(
        ConsumeResult<string, string> result,
        PostCreatedEventV1 payload,
        CancellationToken cancellationToken)
    {
        try
        {
            var outcome = await _processor.ProcessAsync(payload, cancellationToken);
            await _completions.Writer.WriteAsync(new ProcessingCompletion(result, outcome), cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _metrics.RecordFailure("processing");
            _logger.LogWarning(ex, "Unhandled fanout processing failure for event {EventId}", payload.EventId);
            await _completions.Writer.WriteAsync(new ProcessingCompletion(result, ProcessingOutcome.Failed), cancellationToken);
        }
        finally
        {
            _eventLimiter.Release();
        }
    }

    private async Task DrainCompletionsAsync(CancellationToken cancellationToken)
    {
        while (_completions.Reader.TryRead(out var completion))
        {
            if (completion.Outcome == ProcessingOutcome.Failed)
            {
                _consumer.Seek(completion.Result.TopicPartitionOffset);
                _pauseUntil = DateTimeOffset.UtcNow.Add(_options.FailureBackoff);
                _metrics.RecordBackpressure("failure_backoff");
                continue;
            }

            Commit(completion.Result);
        }

        await Task.Yield();
    }

    private async Task ApplyLagBackpressureAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastLagCheck < _options.KafkaLagCheckInterval)
        {
            return;
        }

        _lastLagCheck = now;

        try
        {
            var totalLag = GetTotalLag();
            _metrics.UpdateKafkaLag(totalLag);

            if (totalLag > _options.KafkaLagThreshold)
            {
                _metrics.RecordBackpressure("kafka_lag");
                PauseConsumption();
                _pauseUntil = now.Add(_options.KafkaLagPause);
                _logger.LogWarning("Kafka lag {Lag} exceeded threshold {Threshold}; pausing consumption", totalLag, _options.KafkaLagThreshold);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Kafka lag");
        }

        await Task.Yield();
    }

    private long GetTotalLag()
    {
        var assignments = _consumer.Assignment;
        if (assignments.Count == 0)
        {
            return 0;
        }

        long totalLag = 0;

        foreach (var partition in assignments)
        {
            var watermark = _consumer.QueryWatermarkOffsets(partition, TimeSpan.FromSeconds(1));
            var position = _consumer.Position(partition);
            if (position == Offset.Unset)
            {
                continue;
            }

            var lag = watermark.High.Value - position.Value;
            if (lag > 0)
            {
                totalLag += lag;
            }
        }

        return totalLag;
    }

    private void PauseConsumption()
    {
        if (_paused)
        {
            return;
        }

        var assignments = _consumer.Assignment;
        if (assignments.Count == 0)
        {
            return;
        }

        _consumer.Pause(assignments);
        _paused = true;
    }

    private void ResumeConsumption()
    {
        if (!_paused)
        {
            return;
        }

        var assignments = _consumer.Assignment;
        if (assignments.Count == 0)
        {
            return;
        }

        _consumer.Resume(assignments);
        _paused = false;
    }

    private async Task<bool> RespectPauseAsync(CancellationToken cancellationToken)
    {
        if (!_pauseUntil.HasValue)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        if (now >= _pauseUntil.Value)
        {
            _pauseUntil = null;
            ResumeConsumption();
            return false;
        }

        var remaining = _pauseUntil.Value - now;
        await Task.Delay(remaining, cancellationToken);
        ResumeConsumption();
        _pauseUntil = null;
        return true;
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

    private sealed record ProcessingCompletion(ConsumeResult<string, string> Result, ProcessingOutcome Outcome);
}
