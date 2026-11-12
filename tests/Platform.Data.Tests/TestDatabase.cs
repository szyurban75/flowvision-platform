namespace Platform.Data.Tests;

/// <summary>
/// Connection strings for integration tests, read from environment variables. When a variable is not set,
/// the test is reported as skipped (never as passed).
/// PLATFORM_TEST_CONNECTION: FLOWVISION2_TEST (has the 42 shared tables).
/// PLATFORM_TEST_EMPTY_CONNECTION: an empty scratch database for migration and row-version tests.
/// </summary>
internal static class TestDatabase
{
    public static string? SharedTestConnection => Environment.GetEnvironmentVariable("PLATFORM_TEST_CONNECTION");

    public static string? EmptyConnection => Environment.GetEnvironmentVariable("PLATFORM_TEST_EMPTY_CONNECTION");

    /// <summary>Finds specs/008-multiservice-migration/schema-tables.txt by walking up from the test output folder.</summary>
    public static string FindSchemaTablesFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "specs", "008-multiservice-migration", "schema-tables.txt");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new FileNotFoundException("schema-tables.txt not found above the test folder.");
    }
}
