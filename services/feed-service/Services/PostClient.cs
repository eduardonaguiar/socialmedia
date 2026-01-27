using System.Net.Http.Json;
using FeedService.Models;

namespace FeedService.Services;

public sealed class PostClient
{
    private readonly HttpClient _httpClient;
    private readonly RetryPolicy _retryPolicy;
    private readonly RetrySettings _retrySettings;

    public PostClient(HttpClient httpClient, RetryPolicy retryPolicy, FeedResilienceOptions resilienceOptions)
    {
        _httpClient = httpClient;
        _retryPolicy = retryPolicy;
        _retrySettings = resilienceOptions.PostRetry;
    }

    public async Task<AuthorPostsPage> GetAuthorPostsAsync(
        string authorId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var path = $"/authors/{Uri.EscapeDataString(authorId)}/posts?limit={limit}";
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return await _retryPolicy.ExecuteAsync(
            async token =>
            {
                var response = await _httpClient.GetAsync(path, token);
                response.EnsureSuccessStatusCode();
                var payload = await response.Content.ReadFromJsonAsync<AuthorPostsPage>(cancellationToken: token);
                return payload ?? new AuthorPostsPage(Array.Empty<AuthorPostDto>(), null);
            },
            _retrySettings,
            "post.author_posts",
            cancellationToken);
    }
}
