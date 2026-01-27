using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PostService.Data;
using PostService.Messaging;
using PostService.Metrics;
using PostService.Models;

var builder = WebApplication.CreateBuilder(args);

const string serviceName = "post-service";

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var postgresSettings = PostgresSettings.FromConfiguration(builder.Configuration);
var dataSource = NpgsqlDataSource.Create(postgresSettings.ConnectionString);

builder.Services.AddSingleton(dataSource);
builder.Services.AddSingleton<PostRepository>();
builder.Services.AddSingleton<OutboxRepository>();
builder.Services.AddSingleton<MigrationRunner>();
builder.Services.AddSingleton<OutboxMetrics>();

var kafkaSettings = KafkaSettings.FromConfiguration(builder.Configuration);
var producerConfig = new ProducerConfig
{
    BootstrapServers = kafkaSettings.Brokers,
    ClientId = kafkaSettings.ClientId,
    SecurityProtocol = kafkaSettings.SecurityProtocol
};

builder.Services.AddSingleton<IProducer<string, string>>(_ =>
    new ProducerBuilder<string, string>(producerConfig).Build());

builder.Services.AddHostedService(provider =>
{
    var outboxRepository = provider.GetRequiredService<OutboxRepository>();
    var metrics = provider.GetRequiredService<OutboxMetrics>();
    var logger = provider.GetRequiredService<ILogger<OutboxPublisherService>>();
    var producer = provider.GetRequiredService<IProducer<string, string>>();

    return new OutboxPublisherService(
        outboxRepository,
        metrics,
        logger,
        producer,
        kafkaSettings.PostCreatedTopic,
        TimeSpan.FromSeconds(kafkaSettings.OutboxPollSeconds),
        TimeSpan.FromSeconds(kafkaSettings.OutboxLockSeconds),
        kafkaSettings.OutboxBatchSize);
});

builder.Services.AddHostedService<OutboxBacklogService>();

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
        .AddNpgsql()
        .AddSource("PostService.OutboxPublisher")
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("PostService.Outbox")
        .AddOtlpExporter());

var app = builder.Build();

await ApplyMigrationsAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/posts", async (
    HttpRequest request,
    CreatePostRequest payload,
    PostRepository repository,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-User-Id", out var userIdValues) ||
        string.IsNullOrWhiteSpace(userIdValues))
    {
        return Results.BadRequest(new ErrorResponse(
            new ErrorDetails("missing_user_id", "X-User-Id header is required.")));
    }

    var authorId = userIdValues.ToString();
    if (!PostContentValidator.TryValidate(payload.Content, out var error))
    {
        return Results.BadRequest(new ErrorResponse(
            new ErrorDetails("invalid_content", error)));
    }

    var postId = Guid.NewGuid();
    var outboxId = Guid.NewGuid();
    var createdAtUtc = DateTime.UtcNow;
    var eventPayload = new PostCreatedEventV1(
        outboxId,
        createdAtUtc,
        postId,
        authorId,
        createdAtUtc,
        schemaVersion: 1);

    var payloadJson = JsonSerializer.Serialize(eventPayload);
    var post = await repository.CreateAsync(
        postId,
        authorId,
        payload.Content,
        createdAtUtc,
        outboxId,
        payloadJson,
        cancellationToken);

    return Results.Created($"/posts/{post.PostId}", post);
});

app.MapGet("/posts/{postId:guid}", async (
    Guid postId,
    PostRepository repository,
    CancellationToken cancellationToken) =>
{
    var post = await repository.GetAsync(postId, cancellationToken);
    if (post is null)
    {
        return Results.NotFound(new ErrorResponse(
            new ErrorDetails("not_found", "Post not found.")));
    }

    return Results.Ok(post);
});

app.MapGet("/authors/{authorId}/posts", async (
    HttpRequest request,
    string authorId,
    PostRepository repository,
    CancellationToken cancellationToken) =>
{
    if (!TryGetLimit(request, out var limit, out var errorResponse))
    {
        return Results.BadRequest(errorResponse);
    }

    var cursorRaw = request.Query["cursor"].ToString();
    DateTime? cursorTimestamp = null;
    Guid? cursorPostId = null;

    if (!string.IsNullOrWhiteSpace(cursorRaw))
    {
        if (!CursorCodec.TryDecode(cursorRaw, out var payload))
        {
            return Results.BadRequest(new ErrorResponse(
                new ErrorDetails("invalid_cursor", "Cursor is invalid.")));
        }

        cursorTimestamp = payload.TimestampUtc;
        cursorPostId = payload.PostId;
    }

    var results = await repository.ListByAuthorAsync(authorId, cursorTimestamp, cursorPostId, limit, cancellationToken);

    var items = results
        .Select(item => new AuthorPostDto(
            item.PostId,
            new DateTimeOffset(item.CreatedAtUtc).ToUnixTimeMilliseconds()))
        .ToList();

    var nextCursor = results.Count == limit
        ? CursorCodec.Encode(new CursorPayload(results[^1].CreatedAtUtc, results[^1].PostId))
        : null;

    return Results.Ok(new AuthorPostsPageResponse(items, nextCursor));
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

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = database,
            Username = username,
            Password = password
        };

        return new PostgresSettings(builder.ConnectionString);
    }
}

internal sealed record KafkaSettings(
    string Brokers,
    string ClientId,
    SecurityProtocol SecurityProtocol,
    string PostCreatedTopic,
    int OutboxPollSeconds,
    int OutboxLockSeconds,
    int OutboxBatchSize)
{
    public static KafkaSettings FromConfiguration(IConfiguration configuration)
    {
        var brokers = configuration["KAFKA_BROKERS"] ?? "localhost:9092";
        var clientId = configuration["KAFKA_CLIENT_ID"] ?? "case1-feed";
        var protocolRaw = configuration["KAFKA_SECURITY_PROTOCOL"] ?? "PLAINTEXT";
        _ = Enum.TryParse(protocolRaw, true, out SecurityProtocol protocol);

        var topic = configuration["KAFKA_TOPIC_POST_CREATED"] ?? "post.created.v1";
        var pollSeconds = int.TryParse(configuration["OUTBOX_POLL_SECONDS"], out var pollParsed)
            ? pollParsed
            : 2;
        var lockSeconds = int.TryParse(configuration["OUTBOX_LOCK_SECONDS"], out var lockParsed)
            ? lockParsed
            : 30;
        var batchSize = int.TryParse(configuration["OUTBOX_BATCH_SIZE"], out var batchParsed)
            ? batchParsed
            : 20;

        return new KafkaSettings(brokers, clientId, protocol, topic, pollSeconds, lockSeconds, batchSize);
    }
}
