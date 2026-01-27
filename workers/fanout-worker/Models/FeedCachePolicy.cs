namespace FanoutWorker.Models;

public static class FeedCachePolicy
{
    public const string KeyPrefix = "case1:feed";

    public static string GetFeedKey(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        return $"{KeyPrefix}:{userId}";
    }

    public static bool TryGetTrimRange(long totalCount, int maxItems, out (long Start, long Stop) range)
    {
        range = default;
        if (maxItems <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxItems), "Max items must be positive.");
        }

        if (totalCount <= maxItems)
        {
            return false;
        }

        range = (0, -(maxItems + 1));
        return true;
    }
}
