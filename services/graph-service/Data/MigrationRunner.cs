using Npgsql;

namespace GraphService.Data;

public sealed class MigrationRunner
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<MigrationRunner> _logger;

    public MigrationRunner(NpgsqlDataSource dataSource, ILogger<MigrationRunner> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task RunAsync(string migrationsPath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(migrationsPath))
        {
            _logger.LogWarning("Migrations path not found: {Path}", migrationsPath);
            return;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureSchemaTableAsync(connection, cancellationToken);

        var applied = await GetAppliedAsync(connection, cancellationToken);
        var files = Directory.GetFiles(migrationsPath, "*.sql")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var version = Path.GetFileName(file);
            if (applied.Contains(version))
            {
                continue;
            }

            var sql = await File.ReadAllTextAsync(file, cancellationToken);
            await using var tx = await connection.BeginTransactionAsync(cancellationToken);
            await using (var cmd = new NpgsqlCommand(sql, connection, tx))
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var insertCmd = new NpgsqlCommand(
                "INSERT INTO schema_migrations(version, applied_at_utc) VALUES (@version, now())",
                connection,
                tx))
            {
                insertCmd.Parameters.AddWithValue("version", version);
                await insertCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
            _logger.LogInformation("Applied migration {Version}", version);
        }
    }

    private static async Task EnsureSchemaTableAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version text PRIMARY KEY,
                applied_at_utc timestamptz NOT NULL
            );
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<HashSet<string>> GetAppliedAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = new NpgsqlCommand("SELECT version FROM schema_migrations", connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            applied.Add(reader.GetString(0));
        }

        return applied;
    }
}
