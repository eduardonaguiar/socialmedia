namespace FanoutWorker.Options;

public sealed record GraphSettings(string BaseUrl, TimeSpan Timeout)
{
    public static GraphSettings FromConfiguration(IConfiguration configuration)
    {
        var baseUrl = configuration["GRAPH_SERVICE_URL"] ?? "http://localhost:8082";
        var timeoutMs = int.TryParse(configuration["GRAPH_HTTP_TIMEOUT_MS"], out var parsedTimeout) ? parsedTimeout : 1500;
        return new GraphSettings(baseUrl, TimeSpan.FromMilliseconds(Math.Max(100, timeoutMs)));
    }
}
