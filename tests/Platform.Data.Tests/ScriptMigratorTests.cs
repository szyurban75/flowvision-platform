using Microsoft.Data.SqlClient;
using Xunit;

namespace Platform.Data.Tests;

public class ScriptMigratorTests
{
    private static readonly string[] AllNames =
    [
        "db_mod_FlowVision_2.0.10.sql", "db_FlowVision_2.0.9.sql", "db_mod_FlowVision_2.0.9.sql",
        "db_FlowVision_2.0.36.sql", "readme.txt",
    ];

    private static readonly string[] ExpectedOrder =
    [
        "db_FlowVision_2.0.9.sql", "db_mod_FlowVision_2.0.9.sql", "db_mod_FlowVision_2.0.10.sql", "db_FlowVision_2.0.36.sql",
    ];

    private static string NewFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "fv-migrations-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    [Fact]
    public void FindPending_SortsByNumericVersionAndFullBeforeModification()
    {
        var folder = NewFolder();
        try
        {
            foreach (var name in AllNames)
            {
                File.WriteAllText(Path.Combine(folder, name), "SELECT 1");
            }

            var pending = ScriptMigrator.FindPending(folder, new Version(2, 0, 8));

            Assert.Equal(
                ExpectedOrder,
                pending.Select(script => script.FileName).ToArray());
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void FindPending_SkipsScriptsAtOrBelowBaseline()
    {
        var folder = NewFolder();
        try
        {
            File.WriteAllText(Path.Combine(folder, "db_mod_FlowVision_2.0.35.sql"), "SELECT 1");
            File.WriteAllText(Path.Combine(folder, "db_mod_FlowVision_2.0.36.sql"), "SELECT 1");

            var pending = ScriptMigrator.FindPending(folder, new Version(2, 0, 35));

            Assert.Single(pending);
            Assert.Equal("db_mod_FlowVision_2.0.36.sql", pending[0].FileName);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void SplitBatches_SplitsOnGoLinesOnly()
    {
        const string sql = "CREATE TABLE a (x int)\r\nGO\r\nINSERT INTO a VALUES (1) -- GO inside text\r\ngo\r\n\r\nSELECT 2\r\n";

        var batches = ScriptMigrator.SplitBatches(sql).ToList();

        Assert.Equal(3, batches.Count);
        Assert.Equal("CREATE TABLE a (x int)", batches[0]);
        Assert.Equal("INSERT INTO a VALUES (1) -- GO inside text", batches[1]);
        Assert.Equal("SELECT 2", batches[2]);
    }

    [SkippableFact]
    public async Task ApplyAsync_RunsPendingScriptsAndRecordsVersion_OnEmptyDatabase()
    {
        var connectionString = TestDatabase.EmptyConnection;
        Skip.If(connectionString is null, "PLATFORM_TEST_EMPTY_CONNECTION is not set.");

        var folder = NewFolder();
        try
        {
            // Test script: creates the version table as 2.0.36 does, then creates one table.
            File.WriteAllText(Path.Combine(folder, "db_mod_FlowVision_2.0.36.sql"), """
                IF OBJECT_ID(N'dbo.PLATFORM_SCHEMA_VERSION', N'U') IS NULL
                  CREATE TABLE dbo.PLATFORM_SCHEMA_VERSION (VERSION varchar(20) NOT NULL PRIMARY KEY,
                    APPLIED_AT datetime2(0) NOT NULL DEFAULT SYSDATETIME(), APPLIED_BY nvarchar(128) NOT NULL DEFAULT SUSER_SNAME());
                GO
                IF OBJECT_ID(N'dbo.MIGRATION_PROBE', N'U') IS NULL CREATE TABLE dbo.MIGRATION_PROBE (ID int NOT NULL);
                """);

            await using (var cleanup = new SqlConnection(connectionString))
            {
                await cleanup.OpenAsync();
                await using var drop = new SqlCommand(
                    "IF OBJECT_ID(N'dbo.PLATFORM_SCHEMA_VERSION') IS NOT NULL DROP TABLE dbo.PLATFORM_SCHEMA_VERSION; " +
                    "IF OBJECT_ID(N'dbo.MIGRATION_PROBE') IS NOT NULL DROP TABLE dbo.MIGRATION_PROBE;", cleanup);
                await drop.ExecuteNonQueryAsync();
            }

            var applied = await new ScriptMigrator(connectionString).ApplyAsync(folder, new Version(2, 0, 35));
            Assert.Single(applied);
            Assert.Equal("db_mod_FlowVision_2.0.36.sql", applied[0]);

            await using var verify = new SqlConnection(connectionString);
            await verify.OpenAsync();
            await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.PLATFORM_SCHEMA_VERSION WHERE VERSION = '2.0.36'", verify);
            Assert.Equal(1, (int)(await command.ExecuteScalarAsync())!);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
