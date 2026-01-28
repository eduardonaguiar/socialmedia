using System.Text.Json;
using System.Text.Json.Serialization;
using GraphService.Data;
using GraphService.Metrics;
using GraphService.Models;
using GraphService.Services;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

const string serviceName = "graph-service";

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var postgresSettings = PostgresSettings.FromConfiguration(builder.Configuration);
var celebritySettings = CelebritySettings.FromConfiguration(builder.Configuration);
var resilienceOptions = DatabaseResilienceOptions.FromConfiguration(builder.Configuration);
var dataSource = NpgsqlDataSource.Create(postgresSettings.ConnectionString);

builder.Services.AddSingleton(dataSource);
builder.Services.AddSingleton(resilienceOptions);
builder.Services.AddSingleton<GraphRepository>();
builder.Services.AddSingleton<MigrationRunner>();
builder.Services.AddSingleton<GraphMetrics>();
builder.Services.AddSingleton<DatabaseResilience>();

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
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource(GraphTelemetry.ActivitySourceName)
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("GraphService")
        .AddOtlpExporter());

var app = builder.Build();

await ApplyMigrationsAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (DependencyUnavailableException ex)
    {
        app.Logger.LogWarning(ex, "Dependency unavailable: {Dependency}", ex.Dependency);
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
    }
});

app.MapPost("/follow/{targetUserId}", async (
    HttpRequest request,
    string targetUserId,
    GraphRepository repository,
    GraphMetrics metrics,
    CancellationToken cancellationToken) =>
{
    if (!TryGetUserId(request, out var followerId))
    {
        return Results.BadRequest(new ErrorResponse(
            new ErrorDetails("missing_user_id", "X-User-Id header is required.")));
    }

    if (string.IsNullOrWhiteSpace(targetUserId))
    {
        return Results.BadRequest(new ErrorResponse(
            new ErrorDetails("invalid_target", "Target user id is required.")));
    }

    if (string.Equals(followerId, targetUserId, StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new ErrorResponse(
            new ErrorDetails("invalid_follow", "Self-follow is not allowed.")));
    }

    var result = await repository.FollowAsync(followerId, targetUserId, cancellationToken);
    metrics.RecordFollow(result.Created);

    return Results.Ok(new FollowEdgeDto(
        result.Edge.FollowerId,
        result.Edge.FollowedId,
        result.Edge.FollowedAtUtc));
});

app.MapDelete("/follow/{targetUserId}", async (
    HttpRequest request,
    string targetUserId,
    GraphRepository repository,
    GraphMetrics metrics,
    CancellationToken cancellationToken) =>
{
    if (!TryGetUserId(request, out var followerId))
    {
        return Results.BadRequest(new ErrorResponse(
            new ErrorDetails("missing_user_id", "X-User-Id header is required.")));
    }

    if (string.IsNullOrWhiteSpace(targetUserId))
    {
        return Results.BadRequest(new ErrorResponse(
            new ErrorDetails("invalid_target", "Target user id is required.")));
    }

    if (string.Equals(followerId, targetUserId, StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new ErrorResponse(
            new ErrorDetails("invalid_follow", "Self-follow is not allowed.")));
    }

    var removed = await repository.UnfollowAsync(followerId, targetUserId, cancellationToken);
    metrics.RecordUnfollow(removed);

    return Results.NoContent();
});

app.MapGet("/users/{userId}/following", async (
    HttpRequest request,
    string userId,
    GraphRepository repository,
    GraphMetrics metrics,
    CancellationToken cancellationToken) =>
{
    if (!TryGetLimit(request, out var limit, out var errorResponse))
    {
        return Results.BadRequest(errorResponse);
    }

    var cursorRaw = request.Query["cursor"].ToString();
    DateTime? cursorTimestamp = null;
    string? cursorId = null;

    if (!string.IsNullOrWhiteSpace(cursorRaw))
    {
        if (!CursorCodec.TryDecode(cursorRaw, out var payload))
        {
            return Results.BadRequest(new ErrorResponse(
                new ErrorDetails("invalid_cursor", "Cursor is invalid.")));
        }

        cursorTimestamp = payload.TimestampUtc;
        cursorId = payload.Id;
    }

    using var timer = metrics.TrackList("following");
    var results = await repository.ListFollowingAsync(userId, cursorTimestamp, cursorId, limit, cancellationToken);

    var items = results
        .Select(item => new FollowingDto(item.FollowedId, item.FollowedAtUtc))
        .ToList();

    var nextCursor = results.Count == limit
        ? CursorCodec.Encode(new CursorPayload(results[^1].FollowedAtUtc, results[^1].FollowedId))
        : null;

    return Results.Ok(new PageResponse<FollowingDto>(items, nextCursor));
});

app.MapGet("/users/{userId}/following/celebrity", async (
    HttpRequest request,
    string userId,
    GraphRepository repository,
    GraphMetrics metrics,
    CancellationToken cancellationToken) =>
{
    if (!TryGetLimit(request, out var limit, out var errorResponse))
    {
        return Results.BadRequest(errorResponse);
    }

    var cursorRaw = request.Query["cursor"].ToString();
    DateTime? cursorTimestamp = null;
    string? cursorId = null;

    if (!string.IsNullOrWhiteSpace(cursorRaw))
    {
        if (!CursorCodec.TryDecode(cursorRaw, out var payload))
        {
            return Results.BadRequest(new ErrorResponse(
                new ErrorDetails("invalid_cursor", "Cursor is invalid.")));
        }

        cursorTimestamp = payload.TimestampUtc;
        cursorId = payload.Id;
    }

    using var timer = metrics.TrackList("celebrity_following");
    var results = await repository.ListCelebrityFollowingAsync(
        userId,
        cursorTimestamp,
        cursorId,
        limit,
        celebritySettings.CelebrityFollowerThreshold,
        cancellationToken);

    var items = results
        .Select(item => new CelebrityFollowingDto(item.FollowedId, item.FollowedAtUtc, item.FollowersCount))
        .ToList();

    var nextCursor = results.Count == limit
        ? CursorCodec.Encode(new CursorPayload(results[^1].FollowedAtUtc, results[^1].FollowedId))
        : null;

    return Results.Ok(new PageResponse<CelebrityFollowingDto>(items, nextCursor));
});

app.MapGet("/users/{userId}/followers", async (
    HttpRequest request,
    string userId,
    GraphRepository repository,
    GraphMetrics metrics,
    CancellationToken cancellationToken) =>
{
    if (!TryGetLimit(request, out var limit, out var errorResponse))
    {
        return Results.BadRequest(errorResponse);
    }

    var cursorRaw = request.Query["cursor"].ToString();
    DateTime? cursorTimestamp = null;
    string? cursorId = null;

    if (!string.IsNullOrWhiteSpace(cursorRaw))
    {
        if (!CursorCodec.TryDecode(cursorRaw, out var payload))
        {
            return Results.BadRequest(new ErrorResponse(
                new ErrorDetails("invalid_cursor", "Cursor is invalid.")));
        }

        cursorTimestamp = payload.TimestampUtc;
        cursorId = payload.Id;
    }

    using var timer = metrics.TrackList("followers");
    var results = await repository.ListFollowersAsync(userId, cursorTimestamp, cursorId, limit, cancellationToken);

    var items = results
        .Select(item => new FollowerDto(item.FollowerId, item.FollowedAtUtc))
        .ToList();

    var nextCursor = results.Count == limit
        ? CursorCodec.Encode(new CursorPayload(results[^1].FollowedAtUtc, results[^1].FollowerId))
        : null;

    return Results.Ok(new PageResponse<FollowerDto>(items, nextCursor));
});

app.MapGet("/users/{userId}/stats", async (
    string userId,
    GraphRepository repository,
    CancellationToken cancellationToken) =>
{
    var stats = await repository.GetUserStatsAsync(userId, cancellationToken);
    return Results.Ok(new UserStatsDto(stats.UserId, stats.FollowersCount));
});

app.MapGet("/edges/{followerId}/{targetUserId}", async (
    string followerId,
    string targetUserId,
    GraphRepository repository,
    CancellationToken cancellationToken) =>
{
    var exists = await repository.EdgeExistsAsync(followerId, targetUserId, cancellationToken);
    return Results.Ok(new EdgeStatusResponse(exists));
});

app.MapGet("/health", async (NpgsqlDataSource source, CancellationToken cancellationToken) =>
{
    try
    {
        await using var connection = await source.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand("SELECT 1", connection);
        await cmd.ExecuteScalarAsync(cancellationToken);
        return Results.Ok(new { status = "ok" });
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();

static async Task ApplyMigrationsAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var migrator = scope.ServiceProvider.GetRequiredService<MigrationRunner>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var migrationsPath = Path.Combine(AppContext.BaseDirectory, "Migrations");
    logger.LogInformation("Applying migrations from {Path}", migrationsPath);
    await migrator.RunAsync(migrationsPath, CancellationToken.None);
}

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

internal sealed record PostgresSettings(string ConnectionString)
{
    public static PostgresSettings FromConfiguration(IConfiguration configuration)
    {
        var host = configuration["POSTGRES_HOST"] ?? "localhost";
        var port = int.TryParse(configuration["POSTGRES_PORT"], out var parsedPort) ? parsedPort : 5432;
        var database = configuration["POSTGRES_DB"] ?? "case1_feed";
        var username = configuration["POSTGRES_USER"] ?? "case1_feed";
        var password = configuration["POSTGRES_PASSWORD"] ?? "case1_feed";
        var commandTimeout = int.TryParse(configuration["DB_COMMAND_TIMEOUT_SECONDS"], out var parsedTimeout)
            ? parsedTimeout
            : 2;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = database,
            Username = username,
            Password = password,
            CommandTimeout = Math.Max(1, commandTimeout)
        };

        return new PostgresSettings(builder.ConnectionString);
    }
}

internal sealed record CelebritySettings(long CelebrityFollowerThreshold)
{
    public static CelebritySettings FromConfiguration(IConfiguration configuration)
    {
        var threshold = long.TryParse(configuration["CELEBRITY_FOLLOWER_THRESHOLD"], out var parsed)
            ? parsed
            : 100_000;

        return new CelebritySettings(threshold);
    }
}
