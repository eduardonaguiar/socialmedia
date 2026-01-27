using System.Text.Json;
using Confluent.Kafka;
using FanoutWorker.Metrics;
using FanoutWorker.Options;
using FanoutWorker.Services;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

const string serviceName = "fanout-worker";

var kafkaSettings = KafkaSettings.FromConfiguration(builder.Configuration);
var redisSettings = RedisSettings.FromConfiguration(builder.Configuration);
var graphSettings = GraphSettings.FromConfiguration(builder.Configuration);
var fanoutOptions = FanoutOptions.FromConfiguration(builder.Configuration);

builder.Services.AddSingleton(kafkaSettings);
builder.Services.AddSingleton(redisSettings);
builder.Services.AddSingleton(graphSettings);
builder.Services.AddSingleton(fanoutOptions);

builder.Services.AddSingleton(new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
});

var redisOptions = redisSettings.ToConfigurationOptions();
var redisConnection = ConnectionMultiplexer.Connect(redisOptions);

builder.Services.AddSingleton<IConnectionMultiplexer>(redisConnection);

builder.Services.AddHttpClient<GraphClient>(client =>
{
    client.BaseAddress = new Uri(graphSettings.BaseUrl);
});

builder.Services.AddSingleton<FanoutMetrics>();
builder.Services.AddSingleton<FeedWriter>();
builder.Services.AddSingleton<DedupStore>(sp =>
{
    var connection = sp.GetRequiredService<IConnectionMultiplexer>();
    var logger = sp.GetRequiredService<ILogger<DedupStore>>();
    return new DedupStore(connection, fanoutOptions.RedisRetry, logger);
});
builder.Services.AddSingleton<FanoutProcessor>();

builder.Services.AddSingleton<IConsumer<string, string>>(_ =>
{
    var config = new ConsumerConfig
    {
        BootstrapServers = kafkaSettings.Brokers,
        GroupId = kafkaSettings.ConsumerGroupId,
        ClientId = kafkaSettings.ClientId,
        EnableAutoCommit = false,
        AutoOffsetReset = AutoOffsetReset.Earliest,
        SecurityProtocol = kafkaSettings.SecurityProtocol
    };

    return new ConsumerBuilder<string, string>(config).Build();
});

builder.Services.AddHostedService<FanoutWorkerService>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resourceBuilder =>
    {
        resourceBuilder
            .AddService(serviceName)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = builder.Environment.EnvironmentName.ToLowerInvariant()
            });
    })
    .WithTracing(tracing => tracing
        .AddSource(FanoutTelemetry.ActivitySourceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddStackExchangeRedisInstrumentation(redisConnection)
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("FanoutWorker")
        .AddOtlpExporter());

var app = builder.Build();

app.MapGet("/health", async (IConnectionMultiplexer connection, CancellationToken cancellationToken) =>
{
    try
    {
        var db = connection.GetDatabase();
        await db.PingAsync();
        return Results.Ok(new { status = "ok" });
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();
