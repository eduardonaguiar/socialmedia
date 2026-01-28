using System.Text.Json;
using System.Text.Json.Serialization;
using FeedService.Data;
using FeedService.Metrics;
using FeedService.Models;
using FeedService.Services;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

const string serviceName = "feed-service";

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var redisSettings = RedisSettings.FromConfiguration(builder.Configuration);
var feedOptions = FeedOptions.FromConfiguration(builder.Configuration);
var resilienceOptions = FeedResilienceOptions.FromConfiguration(builder.Configuration);
var graphSettings = GraphSettings.FromConfiguration(builder.Configuration);
var postSettings = PostSettings.FromConfiguration(builder.Configuration);
var redisOptions = redisSettings.ToConfigurationOptions();
var redisConnection = ConnectionMultiplexer.Connect(redisOptions);

builder.Services.AddSingleton<IConnectionMultiplexer>(redisConnection);
builder.Services.AddSingleton<FeedRepository>();
builder.Services.AddSingleton<FeedMetrics>();
builder.Services.AddSingleton(feedOptions);
builder.Services.AddSingleton(resilienceOptions);
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<FeedAggregator>();
builder.Services.AddSingleton<RetryPolicy>();

builder.Services.AddHttpClient<GraphClient>(client =>
{
    client.BaseAddress = new Uri(graphSettings.BaseUrl);
    client.Timeout = resilienceOptions.GraphTimeout;
});

builder.Services.AddHttpClient<PostClient>(client =>
{
    client.BaseAddress = new Uri(postSettings.BaseUrl);
    client.Timeout = resilienceOptions.PostTimeout;
});

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
        .AddSource(FeedTelemetry.ActivitySourceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("FeedService")
        .AddOtlpExporter());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/feed", async (
    HttpRequest request,
    FeedAggregator aggregator,
    FeedMetrics metrics,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (!TryGetUserId(request, out var userId))
    {
        return Results.BadRequest(new ErrorResponse(
            new ErrorDetails("missing_user_id", "X-User-Id header is required.")));
    }

    if (!TryGetLimit(request, out var limit, out var errorResponse))
    {
        return Results.BadRequest(errorResponse);
    }

    var cursorRaw = request.Query["cursor"].ToString();
    CursorPayload? cursor = null;

    if (!string.IsNullOrWhiteSpace(cursorRaw))
    {
        if (!CursorCodec.TryDecode(cursorRaw, out var payload))
        {
            return Results.BadRequest(new ErrorResponse(
                new ErrorDetails("invalid_cursor", "Cursor is invalid.")));
        }

        cursor = payload;
    }

    using var timer = metrics.TrackFeedList();

    try
    {
        var merged = await aggregator.GetFeedAsync(userId, cursor, limit, cancellationToken);
        var items = merged
            .Select(entry => new FeedItemDto(entry.PostId, entry.Score))
            .ToList();

        var nextCursor = merged.Count == limit
            ? CursorCodec.Encode(new CursorPayload(merged[^1].Score, merged[^1].PostId))
            : null;

        return Results.Ok(new FeedPageResponse(items, nextCursor));
    }
    catch (RedisException ex)
    {
        logger.LogWarning(ex, "Redis unavailable while serving feed for user {UserId}", userId);
        metrics.RecordPartialResponse("redis_unavailable");
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

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

static bool TryGetUserId(HttpRequest request, out string userId)
{
    userId = string.Empty;
    if (!request.Headers.TryGetValue("X-User-Id", out var values))
    {
        return false;
    }

    var raw = values.ToString();
    if (string.IsNullOrWhiteSpace(raw))
    {
        return false;
    }

    userId = raw;
    return true;
}

static bool TryGetLimit(HttpRequest request, out int limit, out ErrorResponse? error)
{
    error = null;
    limit = PaginationOptions.DefaultLimit;

    if (!request.Query.TryGetValue("limit", out var limitValues) ||
        string.IsNullOrWhiteSpace(limitValues))
    {
        limit = PaginationOptions.DefaultLimit;
        return true;
    }

    if (!int.TryParse(limitValues.ToString(), out var parsedLimit))
    {
        error = new ErrorResponse(new ErrorDetails("invalid_limit", "Limit must be a number."));
        return false;
    }

    limit = PaginationOptions.NormalizeLimit(parsedLimit);
    return true;
}

internal sealed record RedisSettings(string Host, int Port, int ConnectTimeoutMs, int SyncTimeoutMs, int AsyncTimeoutMs)
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

internal sealed record GraphSettings(string BaseUrl)
{
    public static GraphSettings FromConfiguration(IConfiguration configuration)
    {
        var baseUrl = configuration["GRAPH_SERVICE_URL"] ?? "http://localhost:8082";
        return new GraphSettings(baseUrl);
    }
}

internal sealed record PostSettings(string BaseUrl)
{
    public static PostSettings FromConfiguration(IConfiguration configuration)
    {
        var baseUrl = configuration["POST_SERVICE_URL"] ?? "http://localhost:8081";
        return new PostSettings(baseUrl);
    }
}
