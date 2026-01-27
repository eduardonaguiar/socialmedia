using FanoutWorker.Metrics;
using FanoutWorker.Models;
using FanoutWorker.Options;
using FanoutWorker.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FanoutWorker.Tests;

public sealed class FanoutProcessorTests
{
    [Fact]
    public async Task CelebrityAuthorSkipsFanout()
    {
        var graphClient = new FakeGraphClient(
            new UserStatsDto("author-1", 150),
            Array.Empty<FollowerDto>());
        var feedWriter = new FakeFeedWriter();
        var dedupStore = new FakeDedupStore();
        var metrics = new FanoutMetrics();
        var options = CreateOptions(celebrityThreshold: 100);

        var processor = new FanoutProcessor(
            graphClient,
            feedWriter,
            dedupStore,
            metrics,
            options,
            NullLogger<FanoutProcessor>.Instance);

        var payload = new PostCreatedEventV1(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Guid.NewGuid(),
            "author-1",
            DateTime.UtcNow,
            1);

        var outcome = await processor.ProcessAsync(payload, CancellationToken.None);

        Assert.Equal(ProcessingOutcome.Processed, outcome);
        Assert.Equal(0, feedWriter.Writes);
        Assert.False(graphClient.FollowersRequested);
    }

    [Fact]
    public async Task NormalAuthorFansOutToFollowers()
    {
        var graphClient = new FakeGraphClient(
            new UserStatsDto("author-2", 10),
            new[] { new FollowerDto("follower-1", DateTime.UtcNow) });
        var feedWriter = new FakeFeedWriter();
        var dedupStore = new FakeDedupStore();
        var metrics = new FanoutMetrics();
        var options = CreateOptions(celebrityThreshold: 100);

        var processor = new FanoutProcessor(
            graphClient,
            feedWriter,
            dedupStore,
            metrics,
            options,
            NullLogger<FanoutProcessor>.Instance);

        var payload = new PostCreatedEventV1(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Guid.NewGuid(),
            "author-2",
            DateTime.UtcNow,
            1);

        var outcome = await processor.ProcessAsync(payload, CancellationToken.None);

        Assert.Equal(ProcessingOutcome.Processed, outcome);
        Assert.Equal(1, feedWriter.Writes);
        Assert.True(graphClient.FollowersRequested);
    }

    private static FanoutOptions CreateOptions(long celebrityThreshold)
    {
        return new FanoutOptions(
            HotWindowMaxItems: 1000,
            FollowerPageSize: 200,
            MaxFollowerPages: null,
            DedupTtl: TimeSpan.FromDays(1),
            GraphRetry: new RetrySettings(1, 1, 1),
            RedisRetry: new RetrySettings(1, 1, 1),
            FailureBackoff: TimeSpan.FromMilliseconds(1),
            CelebrityFollowerThreshold: celebrityThreshold);
    }

    private sealed class FakeGraphClient : IGraphClient
    {
        private readonly UserStatsDto _stats;
        private readonly IReadOnlyList<FollowerDto> _followers;

        public FakeGraphClient(UserStatsDto stats, IReadOnlyList<FollowerDto> followers)
        {
            _stats = stats;
            _followers = followers;
        }

        public bool FollowersRequested { get; private set; }

        public Task<UserStatsDto> GetUserStatsAsync(string userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_stats);
        }

        public async IAsyncEnumerable<PageResponse<FollowerDto>> GetFollowersAsync(
            string authorId,
            int pageSize,
            int? maxPages,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            FollowersRequested = true;
            yield return new PageResponse<FollowerDto>(_followers, null);
            await Task.CompletedTask;
        }
    }

    private sealed class FakeFeedWriter : IFeedWriter
    {
        public int Writes { get; private set; }

        public Task AddToFeedAsync(string followerId, Guid postId, long createdAtMs, CancellationToken cancellationToken)
        {
            Writes++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDedupStore : IDedupStore
    {
        public Task<bool> TryClaimAsync(Guid eventId, TimeSpan ttl, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task ReleaseAsync(Guid eventId)
        {
            return Task.CompletedTask;
        }
    }
}
