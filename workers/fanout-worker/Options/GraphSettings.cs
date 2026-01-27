namespace FanoutWorker.Options;

public sealed record GraphSettings(string BaseUrl)
{
    public static GraphSettings FromConfiguration(IConfiguration configuration)
    {
        var baseUrl = configuration["GRAPH_SERVICE_URL"] ?? "http://localhost:8082";
        return new GraphSettings(baseUrl);
    }
}
