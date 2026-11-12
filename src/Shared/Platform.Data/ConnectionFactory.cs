using Microsoft.Data.SqlClient;
using Platform.Secrets;

namespace Platform.Data;

/// <summary>Settings for the database connection, read from configuration section "Database".</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>SQL Server instance, for example "localhost\FI".</summary>
    public string Server { get; set; } = "localhost";

    /// <summary>Database name: FLOWVISION2 (pilot) or FLOWVISION2_TEST (development).</summary>
    public string Database { get; set; } = "FLOWVISION2_TEST";

    /// <summary>"Gmsa" (Windows Integrated Security, primary) or "Dpapi" (SQL login with a protected password).</summary>
    public string AuthMode { get; set; } = "Gmsa";

    /// <summary>SQL login name, used only with AuthMode "Dpapi".</summary>
    public string? SqlUser { get; set; }
}

/// <summary>Builds connection strings. Chooses gMSA or DPAPI from configuration (ADR-001 items 9 to 12).</summary>
public static class ConnectionFactory
{
    public static string Build(DatabaseOptions options, ISecretProvider secrets)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(secrets);

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = options.Server,
            InitialCatalog = options.Database,
            Encrypt = true,
            TrustServerCertificate = false,
            ApplicationName = "FlowVision.Platform",
        };

        switch (options.AuthMode)
        {
            case "Gmsa":
                builder.IntegratedSecurity = true;
                break;

            case "Dpapi":
                if (string.IsNullOrWhiteSpace(options.SqlUser))
                {
                    throw new InvalidOperationException("Database:SqlUser is required when AuthMode is Dpapi.");
                }

                var password = secrets.GetSqlPassword()
                    ?? throw new InvalidOperationException("The protected SQL password could not be read.");
                builder.IntegratedSecurity = false;
                builder.UserID = options.SqlUser;
                builder.Password = password;
                break;

            default:
                throw new InvalidOperationException($"Unknown Database:AuthMode '{options.AuthMode}'. Use Gmsa or Dpapi.");
        }

        return builder.ConnectionString;
    }
}
