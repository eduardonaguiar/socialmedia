namespace FeedService.Models;

public sealed record FeedOptions(
    int PushWindowSize,
    int CelebrityFollowingPageSize,
    int CelebrityFollowingMaxPages,
    int CelebrityPostsPerAuthor,
    int CelebrityPullWindowHours,
    TimeSpan CelebrityListCacheTtl,
    TimeSpan CelebrityListCacheJitter,
    TimeSpan AuthorTimelineCacheTtl,
    TimeSpan AuthorTimelineCacheJitter)
{
    public static FeedOptions FromConfiguration(IConfiguration configuration)
    {
        var pushWindowSize = int.TryParse(configuration["FEED_PUSH_WINDOW_SIZE"], out var parsedWindow)
            ? parsedWindow
            : 200;
        var celebrityFollowingPageSize =
            int.TryParse(configuration["CELEBRITY_FOLLOWING_PAGE_SIZE"], out var parsedFollowingPage)
                ? parsedFollowingPage
                : 100;
        var celebrityFollowingMaxPages =
            int.TryParse(configuration["CELEBRITY_FOLLOWING_MAX_PAGES"], out var parsedFollowingMaxPages)
                ? parsedFollowingMaxPages
                : 5;
        var celebrityPostsPerAuthor =
            int.TryParse(configuration["CELEBRITY_POSTS_PER_AUTHOR"], out var parsedPostsPerAuthor)
                ? parsedPostsPerAuthor
                : 20;
        var celebrityPullWindowHours =
            int.TryParse(configuration["CELEBRITY_PULL_WINDOW_HOURS"], out var parsedWindowHours)
                ? parsedWindowHours
                : 48;
        var celebrityListCacheSeconds =
            int.TryParse(configuration["CELEBRITY_LIST_CACHE_TTL_SECONDS"], out var parsedListTtl)
                ? parsedListTtl
                : 120;
        var celebrityListJitterSeconds =
            int.TryParse(configuration["CELEBRITY_LIST_CACHE_JITTER_SECONDS"], out var parsedListJitter)
                ? parsedListJitter
                : 30;
        var authorTimelineCacheSeconds =
            int.TryParse(configuration["AUTHOR_TIMELINE_CACHE_TTL_SECONDS"], out var parsedTimelineTtl)
                ? parsedTimelineTtl
                : 30;
        var authorTimelineJitterSeconds =
            int.TryParse(configuration["AUTHOR_TIMELINE_CACHE_JITTER_SECONDS"], out var parsedTimelineJitter)
                ? parsedTimelineJitter
                : 10;

        return new FeedOptions(
            pushWindowSize,
            celebrityFollowingPageSize,
            Math.Max(1, celebrityFollowingMaxPages),
            celebrityPostsPerAuthor,
            celebrityPullWindowHours,
            TimeSpan.FromSeconds(celebrityListCacheSeconds),
            TimeSpan.FromSeconds(celebrityListJitterSeconds),
            TimeSpan.FromSeconds(authorTimelineCacheSeconds),
            TimeSpan.FromSeconds(authorTimelineJitterSeconds));
    }
}
