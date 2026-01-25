using System.Text.Json.Serialization;

namespace FeedService.Models;

public sealed record FeedItemDto(
    [property: JsonPropertyName("post_id")] string PostId,
    [property: JsonPropertyName("score")] long Score);

public sealed record FeedPageResponse(
    [property: JsonPropertyName("items")] IReadOnlyList<FeedItemDto> Items,
    [property: JsonPropertyName("next_cursor")] string? NextCursor);

public sealed record FeedEntry(string PostId, long Score);
