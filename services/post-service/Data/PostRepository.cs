using Npgsql;
using PostService.Models;
using PostService.Services;

namespace PostService.Data;

public sealed class PostRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly DatabaseResilience _resilience;

    public PostRepository(NpgsqlDataSource dataSource, DatabaseResilience resilience)
    {
        _dataSource = dataSource;
        _resilience = resilience;
    }

    public async Task<PostDto?> GetAsync(Guid postId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT post_id, author_id, content, created_at_utc
            FROM posts
            WHERE post_id = @post_id
            """;

        return await _resilience.ExecuteAsync(
            async token =>
            {
                await using var connection = await _dataSource.OpenConnectionAsync(token);
                await using var cmd = new NpgsqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("post_id", postId);

                await using var reader = await cmd.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token))
                {
                    return null;
                }

                return new PostDto(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetDateTime(3));
            },
            "post.get",
            cancellationToken);
    }

    public async Task<PostDto> CreateAsync(
        Guid postId,
        string authorId,
        string content,
        DateTime createdAtUtc,
        Guid outboxId,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO posts (post_id, author_id, content, created_at_utc)
            VALUES (@post_id, @author_id, @content, @created_at_utc);

            INSERT INTO outbox_messages (
                outbox_id,
                event_type,
                schema_version,
                payload_json,
                occurred_at_utc,
                publish_attempts
            ) VALUES (
                @outbox_id,
                @event_type,
                @schema_version,
                @payload_json::jsonb,
                @occurred_at_utc,
                0
            );
            """;

        return await _resilience.ExecuteAsync(
            async token =>
            {
                await using var connection = await _dataSource.OpenConnectionAsync(token);
                await using var tx = await connection.BeginTransactionAsync(token);
                await using var cmd = new NpgsqlCommand(sql, connection, tx);
                cmd.Parameters.AddWithValue("post_id", postId);
                cmd.Parameters.AddWithValue("author_id", authorId);
                cmd.Parameters.AddWithValue("content", content);
                cmd.Parameters.AddWithValue("created_at_utc", createdAtUtc);
                cmd.Parameters.AddWithValue("outbox_id", outboxId);
                cmd.Parameters.AddWithValue("event_type", "PostCreated");
                cmd.Parameters.AddWithValue("schema_version", 1);
                cmd.Parameters.AddWithValue("payload_json", payloadJson);
                cmd.Parameters.AddWithValue("occurred_at_utc", createdAtUtc);

                await cmd.ExecuteNonQueryAsync(token);
                await tx.CommitAsync(token);

                return new PostDto(postId, authorId, content, createdAtUtc);
            },
            "post.create",
            cancellationToken);
    }

    public async Task<IReadOnlyList<PostReference>> ListByAuthorAsync(
        string authorId,
        DateTime? cursorTimestampUtc,
        Guid? cursorPostId,
        int limit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT post_id, created_at_utc
            FROM posts
            WHERE author_id = @author_id
              AND (@cursor_ts IS NULL OR (created_at_utc, post_id) < (@cursor_ts, @cursor_id))
            ORDER BY created_at_utc DESC, post_id DESC
            LIMIT @limit;
            """;

        return await _resilience.ExecuteAsync(
            async token =>
            {
                await using var connection = await _dataSource.OpenConnectionAsync(token);
                await using var cmd = new NpgsqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("author_id", authorId);
                cmd.Parameters.AddWithValue("cursor_ts", (object?)cursorTimestampUtc ?? DBNull.Value);
                cmd.Parameters.AddWithValue("cursor_id", (object?)cursorPostId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("limit", limit);

                var results = new List<PostReference>();
                await using var reader = await cmd.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token))
                {
                    results.Add(new PostReference(
                        reader.GetGuid(0),
                        reader.GetDateTime(1)));
                }

                return results;
            },
            "post.list_by_author",
            cancellationToken);
    }
}

public sealed record PostReference(Guid PostId, DateTime CreatedAtUtc);
