using System.Text.Json;
using PostService.Models;
using Xunit;

namespace PostService.Tests;

public class PostCreatedEventV1Tests
{
    [Fact]
    public void SerializesWithSnakeCaseFields()
    {
        var payload = new PostCreatedEventV1(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "author-1",
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            1);

        var json = JsonSerializer.Serialize(payload);

        Assert.Contains("\"event_id\"", json);
        Assert.Contains("\"occurred_at\"", json);
        Assert.Contains("\"post_id\"", json);
        Assert.Contains("\"author_id\"", json);
        Assert.Contains("\"created_at\"", json);
        Assert.Contains("\"schema_version\"", json);
    }
}
