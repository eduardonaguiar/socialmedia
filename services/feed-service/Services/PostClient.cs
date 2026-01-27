using System.Net.Http.Json;
using FeedService.Models;

namespace FeedService.Services;

public sealed class PostClient
{
    private readonly HttpClient _httpClient;

    public PostClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
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

        var response = await _httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AuthorPostsPage>(cancellationToken: cancellationToken);
        return payload ?? new AuthorPostsPage(Array.Empty<AuthorPostDto>(), null);
    }
}
