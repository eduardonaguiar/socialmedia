using System.Net.Http.Json;
using FeedService.Models;

namespace FeedService.Services;

public sealed class GraphClient
{
    private readonly HttpClient _httpClient;

    public GraphClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
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

        var response = await _httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CelebrityFollowingPage>(cancellationToken: cancellationToken);
        return payload ?? new CelebrityFollowingPage(Array.Empty<CelebrityFollowingDto>(), null);
    }
}
