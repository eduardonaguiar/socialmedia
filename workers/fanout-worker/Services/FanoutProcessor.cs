using FanoutWorker.Metrics;
using FanoutWorker.Models;
using FanoutWorker.Options;

namespace FanoutWorker.Services;

public enum ProcessingOutcome
{
    Processed,
    Deduped,
    Failed
}

public sealed class FanoutProcessor
{
    private readonly IGraphClient _graphClient;
    private readonly IFeedWriter _feedWriter;
    private readonly IDedupStore _dedupStore;
    private readonly FanoutMetrics _metrics;
    private readonly FanoutOptions _options;
    private readonly ILogger<FanoutProcessor> _logger;
    private readonly SimpleRateLimiter _followerLimiter;

    public FanoutProcessor(
        IGraphClient graphClient,
        IFeedWriter feedWriter,
        IDedupStore dedupStore,
        FanoutMetrics metrics,
        FanoutOptions options,
        ILogger<FanoutProcessor> logger)
    {
        _graphClient = graphClient;
        _feedWriter = feedWriter;
        _dedupStore = dedupStore;
        _metrics = metrics;
        _options = options;
        _logger = logger;
        _followerLimiter = new SimpleRateLimiter(options.FollowersPerSecond);
    }

    public async Task<ProcessingOutcome> ProcessAsync(PostCreatedEventV1 payload, CancellationToken cancellationToken)
    {
        using var activity = FanoutTelemetry.ActivitySource.StartActivity("fanout.process");
        activity?.SetTag("event.id", payload.EventId.ToString());
        activity?.SetTag("post.id", payload.PostId.ToString());
        activity?.SetTag("author.id", payload.AuthorId);

        using var timer = _metrics.TrackProcessing();

        var claimed = await _dedupStore.TryClaimAsync(payload.EventId, _options.DedupTtl, cancellationToken);
        if (!claimed)
        {
            _metrics.EventsDeduped.Add(1);
            _logger.LogInformation("Deduped event {EventId} for post {PostId}", payload.EventId, payload.PostId);
            return ProcessingOutcome.Deduped;
        }

        var followersProcessed = 0;
        var createdAtMs = new DateTimeOffset(payload.CreatedAtUtc).ToUnixTimeMilliseconds();

        try
        {
            using var classificationTimer = _metrics.TrackCelebrityClassification();
            var stats = await _graphClient.GetUserStatsAsync(payload.AuthorId, cancellationToken);
            var isCelebrity = stats.FollowersCount >= _options.CelebrityFollowerThreshold;

            if (isCelebrity)
            {
                _metrics.EventsProcessed.Add(1);
                _metrics.FanoutSkippedCelebrity.Add(1);
                _logger.LogInformation(
                    "Skipping fanout for celebrity author {AuthorId} followers {FollowersCount} post {PostId}",
                    payload.AuthorId,
                    stats.FollowersCount,
                    payload.PostId);
                return ProcessingOutcome.Processed;
            }

            await foreach (var page in _graphClient.GetFollowersAsync(
                               payload.AuthorId,
                               _options.FollowerPageSize,
                               _options.MaxFollowerPages,
                               cancellationToken))
            {
                foreach (var follower in page.Items)
                {
                    var throttled = await _followerLimiter.WaitAsync(cancellationToken);
                    if (throttled)
                    {
                        _metrics.RecordBackpressure("followers_rate_limit");
                    }

                    await _feedWriter.AddToFeedAsync(
                        follower.FollowerId,
                        payload.PostId,
                        createdAtMs,
                        cancellationToken);
                    followersProcessed++;
                }
            }

            _metrics.EventsProcessed.Add(1);
            _logger.LogInformation(
                "Fanout processed event {EventId} post {PostId} author {AuthorId} followers {FollowersProcessed}",
                payload.EventId,
                payload.PostId,
                payload.AuthorId,
                followersProcessed);

            return ProcessingOutcome.Processed;
        }
        catch (HttpRequestException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _metrics.RecordFailure("graph");
            _logger.LogWarning(ex, "Graph fetch failed for event {EventId}", payload.EventId);
            await _dedupStore.ReleaseAsync(payload.EventId);
            return ProcessingOutcome.Failed;
        }
        catch (StackExchange.Redis.RedisException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _metrics.RecordFailure("redis");
            _logger.LogWarning(ex, "Redis write failed for event {EventId}", payload.EventId);
            await _dedupStore.ReleaseAsync(payload.EventId);
            return ProcessingOutcome.Failed;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _metrics.RecordFailure("processing");
            _logger.LogWarning(ex, "Fanout failed for event {EventId} post {PostId}", payload.EventId, payload.PostId);
            await _dedupStore.ReleaseAsync(payload.EventId);
            return ProcessingOutcome.Failed;
        }
    }
}
