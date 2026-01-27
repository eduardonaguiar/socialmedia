using System.Net.Http.Json;
using FanoutWorker.Metrics;
using FanoutWorker.Models;
using FanoutWorker.Options;

namespace FanoutWorker.Services;

public interface IGraphClient
{
    IAsyncEnumerable<PageResponse<FollowerDto>> GetFollowersAsync(
        string authorId,
        int pageSize,
        int? maxPages,
        CancellationToken cancellationToken);

    Task<UserStatsDto> GetUserStatsAsync(string userId, CancellationToken cancellationToken);
}

public sealed class GraphClient : IGraphClient
{
    private readonly HttpClient _httpClient;
    private readonly FanoutMetrics _metrics;
    private readonly RetrySettings _retrySettings;
    private readonly ILogger<GraphClient> _logger;

    public GraphClient(HttpClient httpClient, FanoutMetrics metrics, FanoutOptions options, ILogger<GraphClient> logger)
    {
        _httpClient = httpClient;
        _metrics = metrics;
        _retrySettings = options.GraphRetry;
        _logger = logger;
    }

    public async IAsyncEnumerable<PageResponse<FollowerDto>> GetFollowersAsync(
        string authorId,
        int pageSize,
        int? maxPages,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? cursor = null;
        var pagesFetched = 0;

        while (true)
        {
            if (maxPages.HasValue && pagesFetched >= maxPages.Value)
            {
                yield break;
            }

            var path = $"/users/{Uri.EscapeDataString(authorId)}/followers?limit={pageSize}";
            if (!string.IsNullOrWhiteSpace(cursor))
            {
                path += $"&cursor={Uri.EscapeDataString(cursor)}";
            }

            using var activity = FanoutTelemetry.ActivitySource.StartActivity("graph.followers");
            activity?.SetTag("graph.author_id", authorId);

            var response = await RetryHelper.ExecuteAsync(
                async token =>
                {
                    var message = await _httpClient.GetAsync(path, token);
                    message.EnsureSuccessStatusCode();
                    var payload = await message.Content.ReadFromJsonAsync<PageResponse<FollowerDto>>(cancellationToken: token);
                    return payload ?? new PageResponse<FollowerDto>(Array.Empty<FollowerDto>(), null);
                },
                _retrySettings,
                _metrics,
                _logger,
                "graph.followers",
                cancellationToken);

            _metrics.FollowerPages.Add(1);
            pagesFetched++;

            yield return response;

            if (string.IsNullOrWhiteSpace(response.NextCursor))
            {
                yield break;
            }

            cursor = response.NextCursor;
        }
    }

    public async Task<UserStatsDto> GetUserStatsAsync(string userId, CancellationToken cancellationToken)
    {
        var path = $"/users/{Uri.EscapeDataString(userId)}/stats";

        using var activity = FanoutTelemetry.ActivitySource.StartActivity("graph.user_stats");
        activity?.SetTag("graph.user_id", userId);

        var response = await RetryHelper.ExecuteAsync(
            async token =>
            {
                var message = await _httpClient.GetAsync(path, token);
                message.EnsureSuccessStatusCode();
                var payload = await message.Content.ReadFromJsonAsync<UserStatsDto>(cancellationToken: token);
                return payload ?? new UserStatsDto(userId, 0);
            },
            _retrySettings,
            _metrics,
            _logger,
            "graph.user_stats",
            cancellationToken);

        return response;
    }
}
