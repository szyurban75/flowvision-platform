using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Platform.Data;

/// <summary>One migration script found on disk.</summary>
public sealed record MigrationScript(string FileName, string FullPath, Version Version, bool IsModification);

/// <summary>
/// Applies db_FlowVision_2.0.N.sql and db_mod_FlowVision_2.0.N.sql scripts in version order (T026, ADR-001).
/// Only scripts newer than the baseline are applied. Each script is split on lines containing only GO.
/// Stops on the first error. Records each applied version in PLATFORM_SCHEMA_VERSION when that table exists.
/// </summary>
public sealed class ScriptMigrator
{
    private static readonly Regex FileNamePattern =
        new(@"^db_(mod_)?FlowVision_(?<version>2\.0\.\d+)\.sql$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex BatchSeparator =
        new(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly string _connectionString;

    public ScriptMigrator(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>Lists scripts in the folder with a version above the baseline, sorted ascending. Same version: full script before modification.</summary>
    public static IReadOnlyList<MigrationScript> FindPending(string scriptFolder, Version baseline)
    {
        if (!Directory.Exists(scriptFolder))
        {
            throw new DirectoryNotFoundException($"Script folder not found: {scriptFolder}");
        }

        return Directory.EnumerateFiles(scriptFolder)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => (name: name!, match: FileNamePattern.Match(name!)))
            .Where(item => item.match.Success)
            .Select(item => new MigrationScript(
                item.name,
                Path.Combine(scriptFolder, item.name),
                Version.Parse(item.match.Groups["version"].Value),
                item.name.Contains("_mod_", StringComparison.OrdinalIgnoreCase)))
            .Where(script => script.Version > baseline)
            .OrderBy(script => script.Version)
            .ThenBy(script => script.IsModification)
            .ToList();
    }

    /// <summary>Splits a script into batches at GO lines. Empty batches are dropped.</summary>
    public static IEnumerable<string> SplitBatches(string sql) =>
        BatchSeparator.Split(sql).Select(batch => batch.Trim()).Where(batch => batch.Length > 0);

    /// <summary>Applies the pending scripts. Returns the file names applied, in order.</summary>
    public async Task<IReadOnlyList<string>> ApplyAsync(string scriptFolder, Version baseline, CancellationToken cancellationToken = default)
    {
        var applied = new List<string>();
        foreach (var script in FindPending(scriptFolder, baseline))
        {
            var sql = await File.ReadAllTextAsync(script.FullPath, cancellationToken);
            await ExecuteScriptAsync(script, sql, cancellationToken);
            await RecordVersionAsync(script.Version, cancellationToken);
            applied.Add(script.FileName);
        }
        return applied;
    }

    private async Task ExecuteScriptAsync(MigrationScript script, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var batch in SplitBatches(sql))
        {
            try
            {
                await using var command = new SqlCommand(batch, connection) { CommandTimeout = 0 };
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqlException exception)
            {
                throw new InvalidOperationException(
                    $"Script {script.FileName} failed: {exception.Message}. Later scripts were not applied.", exception);
            }
        }
    }

    private async Task RecordVersionAsync(Version version, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = """
            IF OBJECT_ID(N'dbo.PLATFORM_SCHEMA_VERSION', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM dbo.PLATFORM_SCHEMA_VERSION WHERE VERSION = @version)
              INSERT INTO dbo.PLATFORM_SCHEMA_VERSION (VERSION) VALUES (@version);
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@version", version.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
