using System.Net.Http.Json;
using System.Text.Json;
using System.Linq;
using Confluent.Kafka;
using Xunit;

namespace FanoutWorker.Tests;

public sealed class FanoutWorkerIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task PostCreatedFansOutToFollowerFeed()
    {
        if (!TryGetSettings(out var settings))
        {
            return;
        }

        using var graphClient = new HttpClient { BaseAddress = new Uri(settings.GraphServiceUrl) };
        using var postClient = new HttpClient { BaseAddress = new Uri(settings.PostServiceUrl) };
        using var feedClient = new HttpClient { BaseAddress = new Uri(settings.FeedServiceUrl) };

        var authorId = Guid.NewGuid().ToString("n");
        var followerId = Guid.NewGuid().ToString("n");

        await CreateFollowAsync(graphClient, followerId, authorId);

        var post = await CreatePostAsync(postClient, authorId);
        var feedItems = await WaitForFeedAsync(feedClient, followerId, post.PostId);

        Assert.Contains(feedItems, item => item.PostId == post.PostId.ToString());
    }

    [Fact]
    public async Task DuplicateEventDoesNotDuplicateFeedItem()
    {
        if (!TryGetSettings(out var settings))
        {
            return;
        }

        using var graphClient = new HttpClient { BaseAddress = new Uri(settings.GraphServiceUrl) };
        using var postClient = new HttpClient { BaseAddress = new Uri(settings.PostServiceUrl) };
        using var feedClient = new HttpClient { BaseAddress = new Uri(settings.FeedServiceUrl) };

        var authorId = Guid.NewGuid().ToString("n");
        var followerId = Guid.NewGuid().ToString("n");

        await CreateFollowAsync(graphClient, followerId, authorId);
        var post = await CreatePostAsync(postClient, authorId);

        var consumed = await ConsumePostCreatedAsync(settings, authorId);
        await PublishDuplicateAsync(settings, consumed);

        var feedItems = await WaitForFeedAsync(feedClient, followerId, post.PostId);
        var occurrences = feedItems.Count(item => item.PostId == post.PostId.ToString());

        Assert.Equal(1, occurrences);
    }

    private static async Task CreateFollowAsync(HttpClient client, string followerId, string authorId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/follow/{authorId}");
        request.Headers.Add("X-User-Id", followerId);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<PostDto> CreatePostAsync(HttpClient client, string authorId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/posts");
        request.Headers.Add("X-User-Id", authorId);
        request.Content = JsonContent.Create(new CreatePostRequest("hello from integration test"));
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PostDto>(JsonOptions);
        return payload ?? throw new InvalidOperationException("Missing post response.");
    }

    private static async Task<List<FeedItemDto>> WaitForFeedAsync(HttpClient client, string followerId, Guid postId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (DateTime.UtcNow < deadline)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/feed?limit=10");
            request.Headers.Add("X-User-Id", followerId);
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<FeedPageResponse>(JsonOptions);
            if (payload is not null && payload.Items.Any(item => item.PostId == postId.ToString()))
            {
                return payload.Items.ToList();
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("Timed out waiting for feed update.");
    }

    private static async Task<ConsumeResult<string, string>> ConsumePostCreatedAsync(TestSettings settings, string authorId)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = settings.KafkaBrokers,
            GroupId = $"fanout-worker-tests-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(settings.Topic);

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var result = consumer.Consume(TimeSpan.FromMilliseconds(500));
            if (result?.Message is null)
            {
                continue;
            }

            if (string.Equals(result.Message.Key, authorId, StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }
        }

        throw new TimeoutException("Timed out waiting for PostCreated event.");
    }

    private static async Task PublishDuplicateAsync(TestSettings settings, ConsumeResult<string, string> message)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = settings.KafkaBrokers,
            ClientId = "fanout-worker-tests"
        };

        using var producer = new ProducerBuilder<string, string>(config).Build();
        await producer.ProduceAsync(settings.Topic, new Message<string, string>
        {
            Key = message.Message.Key,
            Value = message.Message.Value
        });
    }

    private static bool TryGetSettings(out TestSettings settings)
    {
        var graphUrl = Environment.GetEnvironmentVariable("GRAPH_SERVICE_URL") ?? string.Empty;
        var postUrl = Environment.GetEnvironmentVariable("POST_SERVICE_URL") ?? string.Empty;
        var feedUrl = Environment.GetEnvironmentVariable("FEED_SERVICE_URL") ?? string.Empty;
        var kafkaBrokers = Environment.GetEnvironmentVariable("KAFKA_BROKERS") ?? string.Empty;
        var topic = Environment.GetEnvironmentVariable("KAFKA_TOPIC_POST_CREATED") ?? "post.created.v1";

        if (string.IsNullOrWhiteSpace(graphUrl) ||
            string.IsNullOrWhiteSpace(postUrl) ||
            string.IsNullOrWhiteSpace(feedUrl) ||
            string.IsNullOrWhiteSpace(kafkaBrokers))
        {
            settings = default!;
            return false;
        }

        settings = new TestSettings(graphUrl, postUrl, feedUrl, kafkaBrokers, topic);
        return true;
    }

    private sealed record TestSettings(
        string GraphServiceUrl,
        string PostServiceUrl,
        string FeedServiceUrl,
        string KafkaBrokers,
        string Topic);

    private sealed record CreatePostRequest(string Content);

    private sealed record PostDto(Guid PostId, string AuthorId, string Content, DateTime CreatedAtUtc);

    private sealed record FeedItemDto(string PostId, long Score);

    private sealed record FeedPageResponse(IReadOnlyList<FeedItemDto> Items, string? NextCursor);
}
