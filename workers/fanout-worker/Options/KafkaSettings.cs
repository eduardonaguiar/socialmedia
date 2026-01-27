using Confluent.Kafka;

namespace FanoutWorker.Options;

public sealed record KafkaSettings(
    string Brokers,
    string TopicPostCreated,
    string ConsumerGroupId,
    string ClientId,
    SecurityProtocol SecurityProtocol)
{
    public static KafkaSettings FromConfiguration(IConfiguration configuration)
    {
        var brokers = configuration["KAFKA_BROKERS"] ?? "localhost:9092";
        var topic = configuration["KAFKA_TOPIC_POST_CREATED"] ?? "post.created.v1";
        var groupId = configuration["KAFKA_CONSUMER_GROUP_ID"] ?? "case1-feed-fanout-worker";
        var clientId = configuration["KAFKA_CLIENT_ID"] ?? "case1-feed-fanout-worker";
        var protocolRaw = configuration["KAFKA_SECURITY_PROTOCOL"] ?? "PLAINTEXT";

        var protocol = Enum.TryParse<SecurityProtocol>(protocolRaw, true, out var parsed)
            ? parsed
            : SecurityProtocol.Plaintext;

        return new KafkaSettings(brokers, topic, groupId, clientId, protocol);
    }
}
