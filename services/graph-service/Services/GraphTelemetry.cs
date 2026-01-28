using System.Diagnostics;
using Npgsql;

namespace GraphService.Services;

public static class GraphTelemetry
{
    public const string ActivitySourceName = "GraphService.Database";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static Activity? StartDatabaseActivity(
        string operation,
        NpgsqlConnection? connection,
        string? statement = null)
    {
        var activity = ActivitySource.StartActivity("db.query", ActivityKind.Client);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag("db.system", "postgresql");
        activity.SetTag("db.operation", operation);

        if (!string.IsNullOrWhiteSpace(connection?.Database))
        {
            activity.SetTag("db.name", connection.Database);
        }

        if (!string.IsNullOrWhiteSpace(statement))
        {
            activity.SetTag("db.statement", statement);
        }

        return activity;
    }
}
