using System.Text.Json.Serialization;

namespace GraphService.Models;

public sealed record FollowEdgeDto(
    [property: JsonPropertyName("follower_id")] string FollowerId,
    [property: JsonPropertyName("target_user_id")] string TargetUserId,
    [property: JsonPropertyName("followed_at_utc")] DateTime FollowedAtUtc);

public sealed record FollowingDto(
    [property: JsonPropertyName("followed_id")] string FollowedId,
    [property: JsonPropertyName("followed_at_utc")] DateTime FollowedAtUtc);

public sealed record CelebrityFollowingDto(
    [property: JsonPropertyName("followed_id")] string FollowedId,
    [property: JsonPropertyName("followed_at_utc")] DateTime FollowedAtUtc,
    [property: JsonPropertyName("followers_count")] long FollowersCount);

public sealed record FollowerDto(
    [property: JsonPropertyName("follower_id")] string FollowerId,
    [property: JsonPropertyName("followed_at_utc")] DateTime FollowedAtUtc);

public sealed record UserStatsDto(
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("followers_count")] long FollowersCount);

public sealed record PageResponse<T>(
    [property: JsonPropertyName("items")] IReadOnlyList<T> Items,
    [property: JsonPropertyName("next_cursor")] string? NextCursor);

public sealed record EdgeStatusResponse(
    [property: JsonPropertyName("is_following")] bool IsFollowing);
