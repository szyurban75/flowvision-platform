using Microsoft.Data.SqlClient;
using Xunit;

namespace Platform.Data.Tests;

/// <summary>T025: every shared table in schema-tables.txt has a ROWVER rowversion column in FLOWVISION2_TEST.</summary>
public class SchemaTests
{
    [SkippableFact]
    public async Task EverySharedTableHasRowVersionColumn()
    {
        var connectionString = TestDatabase.SharedTestConnection;
        Skip.If(connectionString is null, "PLATFORM_TEST_CONNECTION is not set.");

        var tables = File.ReadAllLines(TestDatabase.FindSchemaTablesFile())
            .Where(line => !line.StartsWith('#') && line.Trim().Length > 0)
            .Select(line => line.Trim())
            .ToList();
        Assert.NotEmpty(tables);

        var withRowVersion = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT t.name FROM sys.tables t JOIN sys.columns c ON c.object_id = t.object_id " +
            "WHERE c.name = 'ROWVER' AND c.system_type_id = 189", connection); // 189 = timestamp/rowversion
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            withRowVersion.Add(reader.GetString(0));
        }

        var missing = tables.Where(table => !withRowVersion.Contains(table)).ToList();
        Assert.True(missing.Count == 0, "Tables without ROWVER: " + string.Join(", ", missing));
    }
}
