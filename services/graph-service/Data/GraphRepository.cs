using System.Data;
using GraphService.Services;
using Npgsql;

namespace GraphService.Data;

public sealed record FollowEdge(string FollowerId, string FollowedId, DateTime FollowedAtUtc);

public sealed record FollowResult(FollowEdge Edge, bool Created);

public sealed record FollowingRecord(string FollowedId, DateTime FollowedAtUtc);

public sealed record FollowerRecord(string FollowerId, DateTime FollowedAtUtc);

public sealed record CelebrityFollowingRecord(string FollowedId, DateTime FollowedAtUtc, long FollowersCount);

public sealed record UserStatsRecord(string UserId, long FollowersCount);

public sealed class GraphRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly DatabaseResilience _resilience;

    public GraphRepository(NpgsqlDataSource dataSource, DatabaseResilience resilience)
    {
        _dataSource = dataSource;
        _resilience = resilience;
    }

    public async Task<FollowResult> FollowAsync(
        string followerId,
        string followedId,
        CancellationToken cancellationToken)
    {
        return await _resilience.ExecuteAsync(
            async token =>
            {
                await using var connection = await _dataSource.OpenConnectionAsync(token);
                await using var tx = await connection.BeginTransactionAsync(token);

                var now = DateTime.UtcNow;
                var (followedAtUtc, created) = await InsertEdgeAsync(
                    connection,
                    tx,
                    followerId,
                    followedId,
                    now,
                    token);

                await UpsertFollowingAsync(connection, tx, followerId, followedId, followedAtUtc, token);
                await UpsertFollowersAsync(connection, tx, followedId, followerId, followedAtUtc, token);
                if (created)
                {
                    await IncrementFollowersCountAsync(connection, tx, followedId, token);
                }

                await tx.CommitAsync(token);

                return new FollowResult(new FollowEdge(followerId, followedId, followedAtUtc), created);
            },
            "graph.follow",
            cancellationToken);
    }

    public async Task<bool> UnfollowAsync(
        string followerId,
        string followedId,
        CancellationToken cancellationToken)
    {
        return await _resilience.ExecuteAsync(
            async token =>
            {
                await using var connection = await _dataSource.OpenConnectionAsync(token);
                await using var tx = await connection.BeginTransactionAsync(token);

                var deleted = await DeleteEdgeAsync(connection, tx, followerId, followedId, token);
                await DeleteFollowingAsync(connection, tx, followerId, followedId, token);
                await DeleteFollowersAsync(connection, tx, followedId, followerId, token);
                if (deleted)
                {
                    await DecrementFollowersCountAsync(connection, tx, followedId, token);
                }

                await tx.CommitAsync(token);

                return deleted;
            },
            "graph.unfollow",
            cancellationToken);
    }

    public async Task<bool> EdgeExistsAsync(
        string followerId,
        string followedId,
        CancellationToken cancellationToken)
    {
        return await _resilience.ExecuteAsync(
            async token =>
            {
                await using var connection = await _dataSource.OpenConnectionAsync(token);
                using var activity = GraphTelemetry.StartDatabaseActivity(
                    "SELECT",
                    connection,
                    "SELECT follow edge");
                await using var cmd = new NpgsqlCommand(
                    "SELECT 1 FROM follow_edges WHERE follower_id = @follower_id AND followed_id = @followed_id",
                    connection);
                cmd.Parameters.AddWithValue("follower_id", followerId);
                cmd.Parameters.AddWithValue("followed_id", followedId);

                var result = await cmd.ExecuteScalarAsync(token);
                return result is not null;
            },
            "graph.edge_exists",
            cancellationToken);
    }

    public async Task<IReadOnlyList<FollowingRecord>> ListFollowingAsync(
        string userId,
        DateTime? cursorTimestampUtc,
        string? cursorId,
        int limit,
        CancellationToken cancellationToken)
    {
        return await _resilience.ExecuteAsync(
            async token =>
            {
                await using var connection = await _dataSource.OpenConnectionAsync(token);
                using var activity = GraphTelemetry.StartDatabaseActivity(
                    "SELECT",
                    connection,
                    "SELECT following list");
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
                await using var reader = await cmd.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token))
                {
                    results.Add(new FollowingRecord(
                        reader.GetString(0),
                        reader.GetDateTime(1)));
                }

                return results;
            },
            "graph.list_following",
            cancellationToken);
    }

    public async Task<IReadOnlyList<FollowerRecord>> ListFollowersAsync(
        string userId,
        DateTime? cursorTimestampUtc,
        string? cursorId,
        int limit,
        CancellationToken cancellationToken)
    {
        return await _resilience.ExecuteAsync(
            async token =>
            {
                await using var connection = await _dataSource.OpenConnectionAsync(token);
                using var activity = GraphTelemetry.StartDatabaseActivity(
                    "SELECT",
                    connection,
                    "SELECT followers list");
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
                await using var reader = await cmd.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token))
                {
                    results.Add(new FollowerRecord(
                        reader.GetString(0),
                        reader.GetDateTime(1)));
                }

                return results;
            },
            "graph.list_followers",
            cancellationToken);
    }

    public async Task<IReadOnlyList<CelebrityFollowingRecord>> ListCelebrityFollowingAsync(
        string userId,
        DateTime? cursorTimestampUtc,
        string? cursorId,
        int limit,
        long celebrityThreshold,
        CancellationToken cancellationToken)
    {
        return await _resilience.ExecuteAsync(
            async token =>
            {
                await using var connection = await _dataSource.OpenConnectionAsync(token);
                using var activity = GraphTelemetry.StartDatabaseActivity(
                    "SELECT",
                    connection,
                    "SELECT celebrity following");
                await using var cmd = new NpgsqlCommand(
                    """
                    SELECT f.followed_id, f.followed_at_utc, COALESCE(s.followers_count, 0) AS followers_count
                    FROM following_by_user f
                    LEFT JOIN user_stats s ON f.followed_id = s.user_id
                    WHERE f.user_id = @user_id
                      AND COALESCE(s.followers_count, 0) >= @threshold
                      AND (@cursor_ts IS NULL OR (f.followed_at_utc, f.followed_id) < (@cursor_ts, @cursor_id))
                    ORDER BY f.followed_at_utc DESC, f.followed_id DESC
                    LIMIT @limit;
                    """,
                    connection);

                cmd.Parameters.AddWithValue("user_id", userId);
                cmd.Parameters.AddWithValue("threshold", celebrityThreshold);
                cmd.Parameters.AddWithValue("cursor_ts", (object?)cursorTimestampUtc ?? DBNull.Value);
                cmd.Parameters.AddWithValue("cursor_id", (object?)cursorId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("limit", limit);

                var results = new List<CelebrityFollowingRecord>();
                await using var reader = await cmd.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token))
                {
                    results.Add(new CelebrityFollowingRecord(
                        reader.GetString(0),
                        reader.GetDateTime(1),
                        reader.GetInt64(2)));
                }

                return results;
            },
            "graph.list_celebrity_following",
            cancellationToken);
    }

    public async Task<UserStatsRecord> GetUserStatsAsync(string userId, CancellationToken cancellationToken)
    {
        return await _resilience.ExecuteAsync(
            async token =>
            {
                await using var connection = await _dataSource.OpenConnectionAsync(token);
                using var activity = GraphTelemetry.StartDatabaseActivity(
                    "SELECT",
                    connection,
                    "SELECT user stats");
                await using var cmd = new NpgsqlCommand(
                    """
                    SELECT user_id, followers_count
                    FROM user_stats
                    WHERE user_id = @user_id;
                    """,
                    connection);

                cmd.Parameters.AddWithValue("user_id", userId);

                await using var reader = await cmd.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    return new UserStatsRecord(reader.GetString(0), reader.GetInt64(1));
                }

                return new UserStatsRecord(userId, 0);
            },
            "graph.user_stats",
            cancellationToken);
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

        using var insertActivity = GraphTelemetry.StartDatabaseActivity(
            "INSERT",
            connection,
            "INSERT follow edge");
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

        using var selectActivity = GraphTelemetry.StartDatabaseActivity(
            "SELECT",
            connection,
            "SELECT follow edge");
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

        using var activity = GraphTelemetry.StartDatabaseActivity(
            "INSERT",
            connection,
            "UPSERT following_by_user");
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

        using var activity = GraphTelemetry.StartDatabaseActivity(
            "INSERT",
            connection,
            "UPSERT followers_by_user");
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

        using var activity = GraphTelemetry.StartDatabaseActivity(
            "DELETE",
            connection,
            "DELETE follow edge");
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

        using var activity = GraphTelemetry.StartDatabaseActivity(
            "DELETE",
            connection,
            "DELETE following_by_user");
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

        using var activity = GraphTelemetry.StartDatabaseActivity(
            "DELETE",
            connection,
            "DELETE followers_by_user");
        cmd.Parameters.AddWithValue("user_id", userId);
        cmd.Parameters.AddWithValue("follower_id", followerId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task IncrementFollowersCountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction tx,
        string userId,
        CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO user_stats (user_id, followers_count)
            VALUES (@user_id, 1)
            ON CONFLICT (user_id)
            DO UPDATE SET followers_count = user_stats.followers_count + 1;
            """,
            connection,
            tx);

        using var activity = GraphTelemetry.StartDatabaseActivity(
            "INSERT",
            connection,
            "UPSERT user_stats increment");
        cmd.Parameters.AddWithValue("user_id", userId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DecrementFollowersCountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction tx,
        string userId,
        CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE user_stats
            SET followers_count = GREATEST(followers_count - 1, 0)
            WHERE user_id = @user_id;
            """,
            connection,
            tx);

        using var activity = GraphTelemetry.StartDatabaseActivity(
            "UPDATE",
            connection,
            "UPDATE user_stats decrement");
        cmd.Parameters.AddWithValue("user_id", userId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
