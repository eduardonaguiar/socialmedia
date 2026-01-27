using System.Diagnostics;

namespace FanoutWorker.Services;

public static class FanoutTelemetry
{
    public const string ActivitySourceName = "FanoutWorker";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
