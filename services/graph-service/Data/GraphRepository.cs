using System.Data;
using Npgsql;

namespace GraphService.Data;

public sealed record FollowEdge(string FollowerId, string FollowedId, DateTime FollowedAtUtc);

public sealed record FollowResult(FollowEdge Edge, bool Created);

public sealed record FollowingRecord(string FollowedId, DateTime FollowedAtUtc);

public sealed record FollowerRecord(string FollowerId, DateTime FollowedAtUtc);

public sealed class GraphRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public GraphRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<FollowResult> FollowAsync(
        string followerId,
        string followedId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var (followedAtUtc, created) = await InsertEdgeAsync(
            connection,
            tx,
            followerId,
            followedId,
            now,
            cancellationToken);

        await UpsertFollowingAsync(connection, tx, followerId, followedId, followedAtUtc, cancellationToken);
        await UpsertFollowersAsync(connection, tx, followedId, followerId, followedAtUtc, cancellationToken);

        await tx.CommitAsync(cancellationToken);

        return new FollowResult(new FollowEdge(followerId, followedId, followedAtUtc), created);
    }

    public async Task<bool> UnfollowAsync(
        string followerId,
        string followedId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);

        var deleted = await DeleteEdgeAsync(connection, tx, followerId, followedId, cancellationToken);
        await DeleteFollowingAsync(connection, tx, followerId, followedId, cancellationToken);
        await DeleteFollowersAsync(connection, tx, followedId, followerId, cancellationToken);

        await tx.CommitAsync(cancellationToken);

        return deleted;
    }

    public async Task<bool> EdgeExistsAsync(
        string followerId,
        string followedId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(
            "SELECT 1 FROM follow_edges WHERE follower_id = @follower_id AND followed_id = @followed_id",
            connection);
        cmd.Parameters.AddWithValue("follower_id", followerId);
        cmd.Parameters.AddWithValue("followed_id", followedId);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }

    public async Task<IReadOnlyList<FollowingRecord>> ListFollowingAsync(
        string userId,
        DateTime? cursorTimestampUtc,
        string? cursorId,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT followed_id, followed_at_utc
            FROM following_by_user
            WHERE user_id = @user_id
              AND (@cursor_ts IS NULL OR (followed_at_utc, followed_id) < (@cursor_ts, @cursor_id))
            ORDER BY followed_at_utc DESC, followed_id DESC
            LIMIT @limit;
            """,
            connection);

        cmd.Parameters.AddWithValue("user_id", userId);
        cmd.Parameters.AddWithValue("cursor_ts", (object?)cursorTimestampUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cursor_id", (object?)cursorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("limit", limit);

        var results = new List<FollowingRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new FollowingRecord(
                reader.GetString(0),
                reader.GetDateTime(1)));
        }

        return results;
    }

    public async Task<IReadOnlyList<FollowerRecord>> ListFollowersAsync(
        string userId,
        DateTime? cursorTimestampUtc,
        string? cursorId,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT follower_id, followed_at_utc
            FROM followers_by_user
            WHERE user_id = @user_id
              AND (@cursor_ts IS NULL OR (followed_at_utc, follower_id) < (@cursor_ts, @cursor_id))
            ORDER BY followed_at_utc DESC, follower_id DESC
            LIMIT @limit;
            """,
            connection);

        cmd.Parameters.AddWithValue("user_id", userId);
        cmd.Parameters.AddWithValue("cursor_ts", (object?)cursorTimestampUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cursor_id", (object?)cursorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("limit", limit);

        var results = new List<FollowerRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new FollowerRecord(
                reader.GetString(0),
                reader.GetDateTime(1)));
        }

        return results;
    }

    private static async Task<(DateTime FollowedAtUtc, bool Created)> InsertEdgeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction tx,
        string followerId,
        string followedId,
        DateTime followedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var insertCmd = new NpgsqlCommand(
            """
            INSERT INTO follow_edges (follower_id, followed_id, followed_at_utc)
            VALUES (@follower_id, @followed_id, @followed_at_utc)
            ON CONFLICT (follower_id, followed_id) DO NOTHING
            RETURNING followed_at_utc;
            """,
            connection,
            tx);

        insertCmd.Parameters.AddWithValue("follower_id", followerId);
        insertCmd.Parameters.AddWithValue("followed_id", followedId);
        insertCmd.Parameters.AddWithValue("followed_at_utc", followedAtUtc);

        var result = await insertCmd.ExecuteScalarAsync(cancellationToken);
        if (result is DateTime insertedAt)
        {
            return (insertedAt, true);
        }

        await using var selectCmd = new NpgsqlCommand(
            """
            SELECT followed_at_utc
            FROM follow_edges
            WHERE follower_id = @follower_id AND followed_id = @followed_id;
            """,
            connection,
            tx);

        selectCmd.Parameters.AddWithValue("follower_id", followerId);
        selectCmd.Parameters.AddWithValue("followed_id", followedId);

        var existing = await selectCmd.ExecuteScalarAsync(cancellationToken);
        if (existing is DateTime existingAt)
        {
            return (existingAt, false);
        }

        throw new DataException("Unable to determine follow timestamp.");
    }

    private static async Task UpsertFollowingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction tx,
        string userId,
        string followedId,
        DateTime followedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO following_by_user (user_id, followed_id, followed_at_utc)
            VALUES (@user_id, @followed_id, @followed_at_utc)
            ON CONFLICT (user_id, followed_id)
            DO UPDATE SET followed_at_utc = EXCLUDED.followed_at_utc;
            """,
            connection,
            tx);

        cmd.Parameters.AddWithValue("user_id", userId);
        cmd.Parameters.AddWithValue("followed_id", followedId);
        cmd.Parameters.AddWithValue("followed_at_utc", followedAtUtc);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertFollowersAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction tx,
        string userId,
        string followerId,
        DateTime followedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO followers_by_user (user_id, follower_id, followed_at_utc)
            VALUES (@user_id, @follower_id, @followed_at_utc)
            ON CONFLICT (user_id, follower_id)
            DO UPDATE SET followed_at_utc = EXCLUDED.followed_at_utc;
            """,
            connection,
            tx);

        cmd.Parameters.AddWithValue("user_id", userId);
        cmd.Parameters.AddWithValue("follower_id", followerId);
        cmd.Parameters.AddWithValue("followed_at_utc", followedAtUtc);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> DeleteEdgeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction tx,
        string followerId,
        string followedId,
        CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            """
            DELETE FROM follow_edges
            WHERE follower_id = @follower_id AND followed_id = @followed_id;
            """,
            connection,
            tx);

        cmd.Parameters.AddWithValue("follower_id", followerId);
        cmd.Parameters.AddWithValue("followed_id", followedId);

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    private static async Task DeleteFollowingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction tx,
        string userId,
        string followedId,
        CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            """
            DELETE FROM following_by_user
            WHERE user_id = @user_id AND followed_id = @followed_id;
            """,
            connection,
            tx);

        cmd.Parameters.AddWithValue("user_id", userId);
        cmd.Parameters.AddWithValue("followed_id", followedId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteFollowersAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction tx,
        string userId,
        string followerId,
        CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            """
            DELETE FROM followers_by_user
            WHERE user_id = @user_id AND follower_id = @follower_id;
            """,
            connection,
            tx);

        cmd.Parameters.AddWithValue("user_id", userId);
        cmd.Parameters.AddWithValue("follower_id", followerId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
