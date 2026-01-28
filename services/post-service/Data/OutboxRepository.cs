using Npgsql;
using PostService.Messaging;
using PostService.Services;

namespace PostService.Data;

public sealed class OutboxRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly DatabaseResilience _resilience;

    public OutboxRepository(NpgsqlDataSource dataSource, DatabaseResilience resilience)
    {
        _dataSource = dataSource;
        _resilience = resilience;
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

        return await _resilience.ExecuteAsync(
            async token =>
            {
                await using var connection = await _dataSource.OpenConnectionAsync(token);
                using var activity = PostTelemetry.StartDatabaseActivity(
                    "UPDATE",
                    connection,
                    "UPDATE outbox lock");
                await using var cmd = new NpgsqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("lock_timeout_seconds", (int)lockTimeout.TotalSeconds);
                cmd.Parameters.AddWithValue("batch_size", batchSize);
                cmd.Parameters.AddWithValue("lock_id", lockId);

                var messages = new List<OutboxMessage>();
                await using var reader = await cmd.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token))
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
            },
            "outbox.lock_pending",
            cancellationToken);
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

        await _resilience.ExecuteAsync(
            async token =>
            {
                await using var connection = await _dataSource.OpenConnectionAsync(token);
                using var activity = PostTelemetry.StartDatabaseActivity(
                    "UPDATE",
                    connection,
                    "UPDATE outbox mark published");
                await using var cmd = new NpgsqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("outbox_id", outboxId);
                await cmd.ExecuteNonQueryAsync(token);
                return true;
            },
            "outbox.mark_published",
            cancellationToken);
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

        await _resilience.ExecuteAsync(
            async token =>
            {
                await using var connection = await _dataSource.OpenConnectionAsync(token);
                using var activity = PostTelemetry.StartDatabaseActivity(
                    "UPDATE",
                    connection,
                    "UPDATE outbox failure");
                await using var cmd = new NpgsqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("outbox_id", outboxId);
                cmd.Parameters.AddWithValue("last_error", error);
                await cmd.ExecuteNonQueryAsync(token);
                return true;
            },
            "outbox.record_failure",
            cancellationToken);
    }

    public async Task<long> GetBacklogAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(*) FROM outbox_messages WHERE published_at_utc IS NULL";
        return await _resilience.ExecuteAsync(
            async token =>
            {
                await using var connection = await _dataSource.OpenConnectionAsync(token);
                using var activity = PostTelemetry.StartDatabaseActivity(
                    "SELECT",
                    connection,
                    "SELECT outbox backlog");
                await using var cmd = new NpgsqlCommand(sql, connection);
                var result = await cmd.ExecuteScalarAsync(token);
                return result is long count ? count : Convert.ToInt64(result ?? 0);
            },
            "outbox.backlog",
            cancellationToken);
    }
}
