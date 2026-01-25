using FeedService.Models;
using Xunit;

namespace FeedService.Tests;

public sealed class FeedCachePolicyTests
{
    [Fact]
    public void TryGetTrimRangeReturnsFalseWhenWithinLimit()
    {
        var result = FeedCachePolicy.TryGetTrimRange(100, 1000, out var range);

        Assert.False(result);
        Assert.Equal(default, range);
    }

    [Fact]
    public void TryGetTrimRangeReturnsRangeWhenOverLimit()
    {
        var result = FeedCachePolicy.TryGetTrimRange(1500, 1000, out var range);

        Assert.True(result);
        Assert.Equal(0, range.Start);
        Assert.Equal(-1001, range.Stop);
    }
}
