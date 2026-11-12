using Platform.Data;

// Usage:
//   MigrationRunner --scripts <folder> --from <version> [--connection <connection string>] [--dry-run]
//
// --from is the version already applied to the database (for example 2.0.35). Only newer scripts run.
// The connection string may also come from the FLOWVISION_MIGRATION_CONNECTION environment variable.
// Run against FLOWVISION2_TEST first. Never run this against the live database without approval.

string? scripts = null, from = null, connection = null;
var dryRun = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--scripts" when i + 1 < args.Length: scripts = args[++i]; break;
        case "--from" when i + 1 < args.Length: from = args[++i]; break;
        case "--connection" when i + 1 < args.Length: connection = args[++i]; break;
        case "--dry-run": dryRun = true; break;
        default:
            Console.Error.WriteLine($"Unknown or incomplete argument: {args[i]}");
            return 2;
    }
}

connection ??= Environment.GetEnvironmentVariable("FLOWVISION_MIGRATION_CONNECTION");

if (scripts is null || from is null || string.IsNullOrWhiteSpace(connection))
{
    Console.Error.WriteLine("Required: --scripts <folder> --from <version> and a connection string (--connection or FLOWVISION_MIGRATION_CONNECTION).");
    return 2;
}

if (!Version.TryParse(from, out var baseline))
{
    Console.Error.WriteLine($"--from must be a version such as 2.0.35, not '{from}'.");
    return 2;
}

var migrator = new ScriptMigrator(connection);

try
{
    if (dryRun)
    {
        var pending = ScriptMigrator.FindPending(scripts, baseline);
        Console.WriteLine($"Dry run. {pending.Count} script(s) would be applied:");
        foreach (var script in pending)
        {
            Console.WriteLine($"  {script.Version}  {script.FileName}");
        }
        return 0;
    }

    var applied = await migrator.ApplyAsync(scripts, baseline);
    Console.WriteLine($"Applied {applied.Count} script(s).");
    foreach (var name in applied)
    {
        Console.WriteLine($"  {name}");
    }
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Migration stopped: {exception.Message}");
    return 1;
}
