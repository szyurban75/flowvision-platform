using Microsoft.AspNetCore.Builder;
using Serilog;

namespace Platform.Logging;

/// <summary>Writes logs to a daily-rolling file (FR-014). The path comes from configuration key "Logging:FilePath".</summary>
public static class FileLoggerSetup
{
    public const string FilePathKey = "Logging:FilePath";

    public static WebApplicationBuilder AddPlatformFileLogging(this WebApplicationBuilder builder, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var path = builder.Configuration[FilePathKey]
            ?? Path.Combine(AppContext.BaseDirectory, "logs", serviceName + ".log");

        builder.Host.UseSerilog((context, configuration) => configuration
            .MinimumLevel.Information()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .WriteTo.File(
                path: path,
                formatProvider: System.Globalization.CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                shared: false));

        return builder;
    }
}
