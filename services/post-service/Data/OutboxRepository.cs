using Npgsql;
using PostService.Messaging;

namespace PostService.Data;

public sealed class OutboxRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public OutboxRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<OutboxMessage>> LockPendingAsync(
        Guid lockId,
        int batchSize,
        TimeSpan lockTimeout,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH pending AS (
                SELECT outbox_id
                FROM outbox_messages
                WHERE published_at_utc IS NULL
                  AND (locked_at_utc IS NULL OR locked_at_utc < (now() - (@lock_timeout_seconds * interval '1 second')))
                ORDER BY occurred_at_utc
                LIMIT @batch_size
                FOR UPDATE SKIP LOCKED
            )
            UPDATE outbox_messages
            SET locked_at_utc = now(),
                lock_id = @lock_id
            WHERE outbox_id IN (SELECT outbox_id FROM pending)
            RETURNING outbox_id, event_type, schema_version, payload_json, occurred_at_utc, publish_attempts;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("lock_timeout_seconds", (int)lockTimeout.TotalSeconds);
        cmd.Parameters.AddWithValue("batch_size", batchSize);
        cmd.Parameters.AddWithValue("lock_id", lockId);

        var messages = new List<OutboxMessage>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            messages.Add(new OutboxMessage(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetDateTime(4),
                reader.GetInt32(5)));
        }

        return messages;
    }

    public async Task MarkPublishedAsync(Guid outboxId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE outbox_messages
            SET published_at_utc = now(),
                locked_at_utc = NULL,
                lock_id = NULL
            WHERE outbox_id = @outbox_id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("outbox_id", outboxId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RecordFailureAsync(Guid outboxId, string error, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE outbox_messages
            SET publish_attempts = publish_attempts + 1,
                last_error = @last_error,
                locked_at_utc = NULL,
                lock_id = NULL
            WHERE outbox_id = @outbox_id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("outbox_id", outboxId);
        cmd.Parameters.AddWithValue("last_error", error);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<long> GetBacklogAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(*) FROM outbox_messages WHERE published_at_utc IS NULL";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, connection);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is long count ? count : Convert.ToInt64(result ?? 0);
    }
}
