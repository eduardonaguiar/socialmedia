using FeedService.Models;
using FeedService.Services;
using Xunit;

namespace FeedService.Tests;

public sealed class FeedMergerTests
{
    [Fact]
    public void MergeOrdersByTimestampThenPostId()
    {
        var pushed = new List<FeedEntry>
        {
            new("post-b", 2000),
            new("post-a", 1000)
        };
        var celebrity = new List<PostReference>
        {
            new("post-c", 2000),
            new("post-d", 1500)
        };

        var merged = FeedMerger.Merge(pushed, celebrity, cursor: null, limit: 10);

        Assert.Equal(new[] { "post-c", "post-b", "post-d", "post-a" }, merged.Select(item => item.PostId));
    }

    [Fact]
    public void MergeDeduplicatesAcrossSources()
    {
        var pushed = new List<FeedEntry>
        {
            new("post-1", 2000)
        };
        var celebrity = new List<PostReference>
        {
            new("post-1", 2000),
            new("post-2", 1500)
        };

        var merged = FeedMerger.Merge(pushed, celebrity, cursor: null, limit: 10);

        Assert.Equal(new[] { "post-1", "post-2" }, merged.Select(item => item.PostId));
    }
}
