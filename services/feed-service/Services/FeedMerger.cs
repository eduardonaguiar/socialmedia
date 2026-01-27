using FeedService.Models;

namespace FeedService.Services;

public static class FeedMerger
{
    public static IReadOnlyList<MergedFeedItem> Merge(
        IReadOnlyList<FeedEntry> pushedEntries,
        IReadOnlyList<PostReference> celebrityEntries,
        CursorPayload? cursor,
        int limit)
    {
        var candidates = new List<MergedFeedItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in pushedEntries)
        {
            if (seen.Add(entry.PostId))
            {
                candidates.Add(new MergedFeedItem(entry.PostId, entry.Score, FeedSource.Pushed));
            }
        }

        foreach (var entry in celebrityEntries)
        {
            if (!IsAfterCursor(entry, cursor))
            {
                continue;
            }

            if (seen.Add(entry.PostId))
            {
                candidates.Add(new MergedFeedItem(entry.PostId, entry.Score, FeedSource.CelebrityPull));
            }
        }

        return candidates
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.PostId, StringComparer.Ordinal)
            .Take(limit)
            .ToList();
    }

    public static bool IsAfterCursor(PostReference entry, CursorPayload? cursor)
    {
        if (cursor is null)
        {
            return true;
        }

        if (entry.Score < cursor.Score)
        {
            return true;
        }

        return entry.Score == cursor.Score && string.CompareOrdinal(entry.PostId, cursor.Member) < 0;
    }
}
