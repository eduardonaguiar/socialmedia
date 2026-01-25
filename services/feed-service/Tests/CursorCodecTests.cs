using FeedService.Models;
using Xunit;

namespace FeedService.Tests;

public sealed class CursorCodecTests
{
    [Fact]
    public void EncodeDecodeRoundTrip()
    {
        var payload = new CursorPayload(1730000000000, "post-123");

        var encoded = CursorCodec.Encode(payload);

        var success = CursorCodec.TryDecode(encoded, out var decoded);

        Assert.True(success);
        Assert.Equal(payload.Score, decoded.Score);
        Assert.Equal(payload.Member, decoded.Member);
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
