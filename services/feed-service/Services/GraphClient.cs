using System.Net.Http.Json;
using FeedService.Models;

namespace FeedService.Services;

public sealed class GraphClient
{
    private readonly HttpClient _httpClient;
    private readonly RetryPolicy _retryPolicy;
    private readonly RetrySettings _retrySettings;

    public GraphClient(HttpClient httpClient, RetryPolicy retryPolicy, FeedResilienceOptions resilienceOptions)
    {
        _httpClient = httpClient;
        _retryPolicy = retryPolicy;
        _retrySettings = resilienceOptions.GraphRetry;
    }

    public async Task<CelebrityFollowingPage> GetCelebrityFollowingAsync(
        string userId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var path = $"/users/{Uri.EscapeDataString(userId)}/following/celebrity?limit={limit}";
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return await _retryPolicy.ExecuteAsync(
            async token =>
            {
                var response = await _httpClient.GetAsync(path, token);
                response.EnsureSuccessStatusCode();
                var payload = await response.Content.ReadFromJsonAsync<CelebrityFollowingPage>(cancellationToken: token);
                return payload ?? new CelebrityFollowingPage(Array.Empty<CelebrityFollowingDto>(), null);
            },
            _retrySettings,
            "graph.celebrity_following",
            cancellationToken);
    }
}
