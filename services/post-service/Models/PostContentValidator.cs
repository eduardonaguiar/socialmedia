namespace PostService.Models;

public static class PostContentValidator
{
    public const int MaxLength = 280;

    public static bool TryValidate(string? content, out string error)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            error = "Content is required.";
            return false;
        }

        if (content.Length > MaxLength)
        {
            error = $"Content must be {MaxLength} characters or fewer.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
