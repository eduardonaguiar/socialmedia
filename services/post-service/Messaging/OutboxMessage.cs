namespace PostService.Messaging;

public sealed record OutboxMessage(
    Guid OutboxId,
    string EventType,
    int SchemaVersion,
    string PayloadJson,
    DateTime OccurredAtUtc,
    int PublishAttempts);
