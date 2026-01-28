using StackExchange.Redis;

namespace FanoutWorker.Options;

public sealed record RedisSettings(string Host, int Port, int ConnectTimeoutMs, int SyncTimeoutMs, int AsyncTimeoutMs)
{
    public static RedisSettings FromConfiguration(IConfiguration configuration)
    {
        var host = configuration["REDIS_HOST"] ?? "localhost";
        var port = int.TryParse(configuration["REDIS_PORT"], out var parsedPort) ? parsedPort : 6379;
        var connectTimeout = int.TryParse(configuration["REDIS_CONNECT_TIMEOUT_MS"], out var parsedConnect) ? parsedConnect : 200;
        var syncTimeout = int.TryParse(configuration["REDIS_SYNC_TIMEOUT_MS"], out var parsedSync) ? parsedSync : 500;
        var asyncTimeout = int.TryParse(configuration["REDIS_ASYNC_TIMEOUT_MS"], out var parsedAsync) ? parsedAsync : 500;

        return new RedisSettings(host, port, connectTimeout, syncTimeout, asyncTimeout);
    }

    public ConfigurationOptions ToConfigurationOptions()
    {
        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectTimeout = ConnectTimeoutMs,
            SyncTimeout = SyncTimeoutMs,
            AsyncTimeout = AsyncTimeoutMs
        };
        options.EndPoints.Add(Host, Port);
        return options;
    }
}
