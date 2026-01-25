using System.Text.Json.Serialization;

namespace PostService.Models;

public sealed record ErrorDetails(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("details")] object? Details = null);

public sealed record ErrorResponse(
    [property: JsonPropertyName("error")] ErrorDetails Error);
