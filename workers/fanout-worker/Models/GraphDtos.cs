using System.Text.Json.Serialization;

namespace FanoutWorker.Models;

public sealed record FollowerDto(
    [property: JsonPropertyName("follower_id")] string FollowerId,
    [property: JsonPropertyName("followed_at_utc")] DateTime FollowedAtUtc);

public sealed record PageResponse<T>(
    [property: JsonPropertyName("items")] IReadOnlyList<T> Items,
    [property: JsonPropertyName("next_cursor")] string? NextCursor);
