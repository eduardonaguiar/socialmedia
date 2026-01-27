using System.Text.Json.Serialization;

namespace PostService.Models;

public sealed record AuthorPostDto(
    [property: JsonPropertyName("post_id")] Guid PostId,
    [property: JsonPropertyName("created_at_ms")] long CreatedAtMs);

public sealed record AuthorPostsPageResponse(
    [property: JsonPropertyName("items")] IReadOnlyList<AuthorPostDto> Items,
    [property: JsonPropertyName("next_cursor")] string? NextCursor);
