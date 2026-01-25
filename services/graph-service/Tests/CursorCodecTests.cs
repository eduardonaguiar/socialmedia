using GraphService.Models;
using Xunit;

namespace GraphService.Tests;

public sealed class CursorCodecTests
{
    [Fact]
    public void EncodeDecodeRoundTrip()
    {
        var now = new DateTime(2024, 04, 11, 10, 30, 0, DateTimeKind.Utc);
        var payload = new CursorPayload(now, "user-123");

        var encoded = CursorCodec.Encode(payload);

        var success = CursorCodec.TryDecode(encoded, out var decoded);

        Assert.True(success);
        Assert.Equal(payload.TimestampUtc, decoded.TimestampUtc);
        Assert.Equal(payload.Id, decoded.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-base64")]
    public void DecodeRejectsInvalidCursors(string cursor)
    {
        var success = CursorCodec.TryDecode(cursor, out _);

        Assert.False(success);
    }
}
