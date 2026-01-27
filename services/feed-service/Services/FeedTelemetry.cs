using System.Diagnostics;

namespace FeedService.Services;

public static class FeedTelemetry
{
    public const string ActivitySourceName = "FeedService";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
