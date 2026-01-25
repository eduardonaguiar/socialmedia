namespace FeedService.Models;

public static class PaginationOptions
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    public static int NormalizeLimit(int? limit)
    {
        if (!limit.HasValue || limit.Value <= 0)
        {
            return DefaultLimit;
        }

        return Math.Min(limit.Value, MaxLimit);
    }
}
