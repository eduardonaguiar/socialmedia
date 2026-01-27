using StackExchange.Redis;

namespace FanoutWorker.Options;

public sealed record RedisSettings(string Host, int Port)
{
    public static RedisSettings FromConfiguration(IConfiguration configuration)
    {
        var host = configuration["REDIS_HOST"] ?? "localhost";
        var port = int.TryParse(configuration["REDIS_PORT"], out var parsedPort) ? parsedPort : 6379;

        return new RedisSettings(host, port);
    }

    public ConfigurationOptions ToConfigurationOptions()
    {
        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false
        };
        options.EndPoints.Add(Host, Port);
        return options;
    }
}
