using PostService.Models;
using Xunit;

namespace PostService.Tests;

public class PostContentValidatorTests
{
    [Fact]
    public void RejectsEmptyContent()
    {
        var result = PostContentValidator.TryValidate(" ", out var error);

        Assert.False(result);
        Assert.Equal("Content is required.", error);
    }

    [Fact]
    public void RejectsTooLongContent()
    {
        var content = new string('a', PostContentValidator.MaxLength + 1);

        var result = PostContentValidator.TryValidate(content, out var error);

        Assert.False(result);
        Assert.Equal($"Content must be {PostContentValidator.MaxLength} characters or fewer.", error);
    }

    [Fact]
    public void AcceptsValidContent()
    {
        var content = new string('a', PostContentValidator.MaxLength);

        var result = PostContentValidator.TryValidate(content, out var error);

        Assert.True(result);
        Assert.Equal(string.Empty, error);
    }
}
