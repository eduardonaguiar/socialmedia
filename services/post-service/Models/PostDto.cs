using System.Text.Json.Serialization;

namespace PostService.Models;

public sealed record PostDto(
    [property: JsonPropertyName("post_id")] Guid PostId,
    [property: JsonPropertyName("author_id")] string AuthorId,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("created_at")] DateTime CreatedAtUtc);
