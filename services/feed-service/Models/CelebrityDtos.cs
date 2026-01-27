using System.Text.Json.Serialization;

namespace FeedService.Models;

public sealed record CelebrityFollowingDto(
    [property: JsonPropertyName("followed_id")] string FollowedId,
    [property: JsonPropertyName("followed_at_utc")] DateTime FollowedAtUtc,
    [property: JsonPropertyName("followers_count")] long FollowersCount);

public sealed record CelebrityFollowingPage(
    [property: JsonPropertyName("items")] IReadOnlyList<CelebrityFollowingDto> Items,
    [property: JsonPropertyName("next_cursor")] string? NextCursor);

public sealed record AuthorPostDto(
    [property: JsonPropertyName("post_id")] string PostId,
    [property: JsonPropertyName("created_at_ms")] long CreatedAtMs);

public sealed record AuthorPostsPage(
    [property: JsonPropertyName("items")] IReadOnlyList<AuthorPostDto> Items,
    [property: JsonPropertyName("next_cursor")] string? NextCursor);

public sealed record PostReference(string PostId, long Score);

public sealed record MergedFeedItem(string PostId, long Score, FeedSource Source);

public enum FeedSource
{
    Pushed,
    CelebrityPull
}
