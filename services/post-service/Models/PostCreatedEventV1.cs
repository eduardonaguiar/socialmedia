using System.Text.Json.Serialization;

namespace PostService.Models;

public sealed record PostCreatedEventV1(
    [property: JsonPropertyName("event_id")] Guid EventId,
    [property: JsonPropertyName("occurred_at")] DateTime OccurredAtUtc,
    [property: JsonPropertyName("post_id")] Guid PostId,
    [property: JsonPropertyName("author_id")] string AuthorId,
    [property: JsonPropertyName("created_at")] DateTime CreatedAtUtc,
    [property: JsonPropertyName("schema_version")] int SchemaVersion);
