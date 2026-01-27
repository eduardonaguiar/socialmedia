using System.Collections.Concurrent;
using FeedService.Data;
using FeedService.Metrics;
using FeedService.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FeedService.Services;

public sealed class FeedAggregator
{
    private readonly FeedRepository _repository;
    private readonly GraphClient _graphClient;
    private readonly PostClient _postClient;
    private readonly IMemoryCache _cache;
    private readonly FeedMetrics _metrics;
    private readonly FeedOptions _options;
    private readonly ILogger<FeedAggregator> _logger;

    public FeedAggregator(
        FeedRepository repository,
        GraphClient graphClient,
        PostClient postClient,
        IMemoryCache cache,
        FeedMetrics metrics,
        FeedOptions options,
        ILogger<FeedAggregator> logger)
    {
        _repository = repository;
        _graphClient = graphClient;
        _postClient = postClient;
        _cache = cache;
        _metrics = metrics;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MergedFeedItem>> GetFeedAsync(
        string userId,
        CursorPayload? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pushedWindow = Math.Max(limit, _options.PushWindowSize);
        var pushedEntries = await _repository.GetFeedPageAsync(userId, cursor, pushedWindow, cancellationToken);

        IReadOnlyList<string> celebrityAuthors;
        using (var activity = FeedTelemetry.ActivitySource.StartActivity("graph.celebrity_following"))
        {
            activity?.SetTag("feed.user_id", userId);
            celebrityAuthors = await GetCelebrityAuthorsAsync(userId, cancellationToken);
        }

        if (celebrityAuthors.Count == 0)
        {
            using var mergeTimer = _metrics.TrackMerge();
            var merged = FeedMerger.Merge(pushedEntries, Array.Empty<PostReference>(), cursor, limit);
            RecordMergeMetrics(merged);
            return merged;
        }

        var celebrityPosts = new ConcurrentBag<PostReference>();
        try
        {
            var windowCutoff = DateTimeOffset.UtcNow.AddHours(-_options.CelebrityPullWindowHours)
                .ToUnixTimeMilliseconds();

            foreach (var authorId in celebrityAuthors)
            {
                using var activity = FeedTelemetry.ActivitySource.StartActivity("post.author_timeline");
                activity?.SetTag("feed.author_id", authorId);

                var posts = await GetAuthorTimelineAsync(authorId, cancellationToken);
                foreach (var post in posts)
                {
                    if (post.Score >= windowCutoff)
                    {
                        celebrityPosts.Add(post);
                    }
                }
            }
        }
        catch (HttpRequestException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _metrics.RecordPullCall("post_service", false);
            _metrics.RecordCelebrityPullFailure("post_service");
            _logger.LogWarning(ex, "Celebrity pull failed for user {UserId}", userId);
            var merged = FeedMerger.Merge(pushedEntries, Array.Empty<PostReference>(), cursor, limit);
            RecordMergeMetrics(merged);
            return merged;
        }

        using var finalMergeTimer = _metrics.TrackMerge();
        var mergedItems = FeedMerger.Merge(pushedEntries, celebrityPosts.ToList(), cursor, limit);
        RecordMergeMetrics(mergedItems);
        return mergedItems;
    }

    private async Task<IReadOnlyList<string>> GetCelebrityAuthorsAsync(string userId, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue<IReadOnlyList<string>>(GetCelebrityCacheKey(userId), out var cached))
        {
            return cached;
        }

        var authors = new List<string>();
        string? cursor = null;

        try
        {
            for (var page = 0; page < _options.CelebrityFollowingMaxPages; page++)
            {
                using var activity = FeedTelemetry.ActivitySource.StartActivity("graph.celebrity_following.page");
                activity?.SetTag("feed.user_id", userId);

                var response = await _graphClient.GetCelebrityFollowingAsync(
                    userId,
                    _options.CelebrityFollowingPageSize,
                    cursor,
                    cancellationToken);
                _metrics.RecordPullCall("graph", true);

                authors.AddRange(response.Items.Select(item => item.FollowedId));

                if (string.IsNullOrWhiteSpace(response.NextCursor))
                {
                    break;
                }

                cursor = response.NextCursor;
            }
        }
        catch (HttpRequestException) when (!cancellationToken.IsCancellationRequested)
        {
            _metrics.RecordPullCall("graph", false);
            _metrics.RecordCelebrityPullFailure("graph_service");
            return Array.Empty<string>();
        }

        CacheWithJitter(GetCelebrityCacheKey(userId), authors, _options.CelebrityListCacheTtl, _options.CelebrityListCacheJitter);
        return authors;
    }

    private async Task<IReadOnlyList<PostReference>> GetAuthorTimelineAsync(string authorId, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue<IReadOnlyList<PostReference>>(GetAuthorCacheKey(authorId), out var cached))
        {
            return cached;
        }

        var response = await _postClient.GetAuthorPostsAsync(
            authorId,
            _options.CelebrityPostsPerAuthor,
            cursor: null,
            cancellationToken);
        _metrics.RecordPullCall("post_service", true);

        var results = response.Items
            .Select(item => new PostReference(item.PostId, item.CreatedAtMs))
            .ToList();

        CacheWithJitter(GetAuthorCacheKey(authorId), results, _options.AuthorTimelineCacheTtl, _options.AuthorTimelineCacheJitter);
        return results;
    }

    private void CacheWithJitter<T>(string key, T value, TimeSpan ttl, TimeSpan jitter)
    {
        var expiration = ttl;
        if (jitter > TimeSpan.Zero)
        {
            var jitterMs = Random.Shared.NextDouble() * jitter.TotalMilliseconds;
            expiration = ttl.Add(TimeSpan.FromMilliseconds(jitterMs));
        }

        _cache.Set(key, value, expiration);
    }

    private void RecordMergeMetrics(IReadOnlyList<MergedFeedItem> mergedItems)
    {
        var pushed = mergedItems.Count(item => item.Source == FeedSource.Pushed);
        var celebrity = mergedItems.Count(item => item.Source == FeedSource.CelebrityPull);

        _metrics.RecordMergeItems("pushed", pushed);
        _metrics.RecordMergeItems("celebrity_pull", celebrity);
    }

    private static string GetCelebrityCacheKey(string userId) => $"feed:celebrity:{userId}";

    private static string GetAuthorCacheKey(string authorId) => $"feed:author:{authorId}";
}
