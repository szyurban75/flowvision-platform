using Microsoft.Data.SqlClient;

namespace Platform.SchemaCheck;

public enum SchemaStatus
{
    /// <summary>The database version equals the version the service expects.</summary>
    Match,

    /// <summary>The database is older than the service expects. Apply the pending migrations first.</summary>
    DatabaseOlder,

    /// <summary>The database is newer than the service expects. Update the service.</summary>
    DatabaseNewer,

    /// <summary>PLATFORM_SCHEMA_VERSION is missing or empty.</summary>
    Unknown,
}

public sealed record SchemaCheckResult(SchemaStatus Status, string Expected, string? Found)
{
    public bool IsCompatible => Status == SchemaStatus.Match;
}

/// <summary>Reads PLATFORM_SCHEMA_VERSION and compares it with the version the service expects (FR-006).</summary>
public static class SchemaVersionChecker
{
    private const string ReadVersionSql =
        "SELECT TOP (1) VERSION FROM dbo.PLATFORM_SCHEMA_VERSION ORDER BY APPLIED_AT DESC";

    /// <summary>Pure comparison, testable without a database.</summary>
    public static SchemaCheckResult Evaluate(string expected, string? found)
    {
        if (string.IsNullOrWhiteSpace(found) || !Version.TryParse(found.Trim(), out var foundVersion))
        {
            return new SchemaCheckResult(SchemaStatus.Unknown, expected, found);
        }

        var expectedVersion = Version.Parse(expected);
        var comparison = foundVersion.CompareTo(expectedVersion);
        var status = comparison switch
        {
            0 => SchemaStatus.Match,
            < 0 => SchemaStatus.DatabaseOlder,
            _ => SchemaStatus.DatabaseNewer,
        };
        return new SchemaCheckResult(status, expected, found.Trim());
    }

    public static async Task<SchemaCheckResult> CheckAsync(
        string connectionString, string expected, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(ReadVersionSql, connection);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Evaluate(expected, value as string);
    }
}
