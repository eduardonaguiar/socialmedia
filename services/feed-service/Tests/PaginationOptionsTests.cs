using FeedService.Models;
using Xunit;

namespace FeedService.Tests;

public sealed class PaginationOptionsTests
{
    [Theory]
    [InlineData(null, PaginationOptions.DefaultLimit)]
    [InlineData(0, PaginationOptions.DefaultLimit)]
    [InlineData(-5, PaginationOptions.DefaultLimit)]
    [InlineData(10, 10)]
    [InlineData(250, PaginationOptions.MaxLimit)]
    public void NormalizeLimitClampsValues(int? input, int expected)
    {
        var normalized = PaginationOptions.NormalizeLimit(input);

        Assert.Equal(expected, normalized);
    }
}
