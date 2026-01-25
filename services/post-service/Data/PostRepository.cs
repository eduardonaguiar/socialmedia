using Npgsql;
using PostService.Models;

namespace PostService.Data;

public sealed class PostRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<PostDto?> GetAsync(Guid postId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT post_id, author_id, content, created_at_utc
            FROM posts
            WHERE post_id = @post_id
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("post_id", postId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PostDto(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetDateTime(3));
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

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
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

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new PostDto(postId, authorId, content, createdAtUtc);
    }
}
