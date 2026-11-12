using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Data;
using Platform.Logging;
using Platform.SchemaCheck;
using Platform.Secrets;

namespace Platform.Hosting;

/// <summary>Shared setup for the four services: file logging, error mapping, database connection, schema check, health (T044).</summary>
public static class PlatformDefaults
{
    private static readonly Action<ILogger, string, string, string, Exception?> RequestFailed =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Error,
            new EventId(1001, "RequestFailed"),
            "Request {Method} {Path} failed with {Code}");

    /// <summary>Configuration section "Platform": ServiceName, ExpectedSchemaVersion, SecretName (DPAPI file name, optional).</summary>
    public const string SectionName = "Platform";

    public static WebApplicationBuilder AddPlatformDefaults(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var serviceName = ServiceName(builder.Configuration);
        builder.AddPlatformFileLogging(serviceName);
        builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
        // All services share one key ring so a cookie issued by Identity can be read by Admin, ShopFloor and Documents.
        builder.Services.AddDataProtection()
            .SetApplicationName("FlowVision")
            .PersistKeysToFileSystem(new DirectoryInfo(DataProtectionKeysPath(builder.Configuration)));
        return builder;
    }

    /// <summary>Folder for the shared key ring. Configuration key Platform:DataProtectionKeysPath; default %ProgramData%\FlowVision\keys.</summary>
    public static string DataProtectionKeysPath(IConfiguration configuration) =>
        configuration[$"{SectionName}:DataProtectionKeysPath"]
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FlowVision", "keys");

    public static string ServiceName(IConfiguration configuration) =>
        configuration[$"{SectionName}:ServiceName"] ?? throw new InvalidOperationException("Platform:ServiceName is not set.");

    public static string ExpectedSchemaVersion(IConfiguration configuration) =>
        configuration[$"{SectionName}:ExpectedSchemaVersion"] ?? throw new InvalidOperationException("Platform:ExpectedSchemaVersion is not set.");

    /// <summary>Builds the connection string from configuration, choosing gMSA or DPAPI.</summary>
    public static string BuildConnectionString(IConfiguration configuration)
    {
        var options = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
        var secretName = configuration[$"{SectionName}:SecretName"];

        ISecretProvider secrets;
        if (options.AuthMode == "Dpapi" && !string.IsNullOrWhiteSpace(secretName))
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("DPAPI secrets are available on Windows only.");
            }
            secrets = new DpapiSecretProvider(DpapiSecretProvider.DefaultPath(secretName));
        }
        else
        {
            secrets = new GmsaConnectionProvider();
        }

        return ConnectionFactory.Build(options, secrets);
    }

    /// <summary>
    /// Refuses to start when PLATFORM_SCHEMA_VERSION does not match the version this service expects (FR-006).
    /// Call once before app.Run().
    /// </summary>
    public static async Task EnsureSchemaVersionAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);
        var expected = ExpectedSchemaVersion(app.Configuration);
        var connectionString = BuildConnectionString(app.Configuration);
        var result = await SchemaVersionChecker.CheckAsync(connectionString, expected, cancellationToken);
        if (!result.IsCompatible)
        {
            throw new InvalidOperationException(
                $"Schema version mismatch: service expects {result.Expected}, database reports {result.Found ?? "none"} ({result.Status}). " +
                "Apply the pending migrations or update the service.");
        }
    }

    /// <summary>Maps GET /api/{service}/health with the status and the expected and found schema versions (contracts, Common).</summary>
    public static WebApplication MapPlatformHealth(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var serviceName = ServiceName(app.Configuration);
        var expected = ExpectedSchemaVersion(app.Configuration);

        app.MapGet($"/api/{serviceName}/health", async (CancellationToken cancellationToken) =>
        {
            var connectionString = BuildConnectionString(app.Configuration);
            var check = await SchemaVersionChecker.CheckAsync(connectionString, expected, cancellationToken);
            return Results.Json(new
            {
                status = check.IsCompatible ? "ok" : "degraded",
                service = serviceName,
                expectedSchemaVersion = expected,
                foundSchemaVersion = check.Found,
            });
        });
        return app;
    }

    /// <summary>Converts unhandled exceptions to the plain-language response from ErrorMapper. No stack trace is returned.</summary>
    public static WebApplication UsePlatformErrorMapping(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Use(async (HttpContext context, RequestDelegate next) =>
        {
            try
            {
                await next(context);
            }
            catch (Exception exception) when (!context.Response.HasStarted)
            {
                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Platform.ErrorMapping");
                var mapped = ErrorMapper.Map(exception);
                RequestFailed(logger, context.Request.Method, context.Request.Path.ToString(), mapped.Code, exception);

                context.Response.StatusCode = mapped.Status;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { code = mapped.Code, message = mapped.Message }));
            }
        });
        return app;
    }
}
