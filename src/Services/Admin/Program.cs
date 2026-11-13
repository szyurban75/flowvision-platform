using Admin.Api;
using Microsoft.EntityFrameworkCore;
using Platform.Auth;
using Platform.Data;
using Platform.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();

var connectionString = PlatformDefaults.BuildConnectionString(builder.Configuration);
builder.Services.AddDbContext<AuthDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddSingleton<IPasswordVerifier, PasswordVerifier>();

builder.Services.AddAuthentication().AddInterfaceCookies();
builder.Services.AddAuthorization(options => options.AddPolicy(PasswordResetEndpoints.PolicyName, policy => policy
    .AddAuthenticationSchemes(InterfaceCookies.SchemeFor(Interfaces.Admin))
    .RequireAuthenticatedUser()
    .RequireClaim(InterfaceCookies.RoleClaim, RoleRequirements.Administrator)));

var app = builder.Build();

app.UsePlatformErrorMapping();
app.UseAuthentication();
app.UseAuthorization();

app.MapPlatformHealth();
app.MapPasswordResetEndpoints();

// Refuses to start if PLATFORM_SCHEMA_VERSION does not match Platform:ExpectedSchemaVersion (FR-006).
// Integration tests run without SQL Server, so they use the "IntegrationTests" environment and skip this check.
if (!app.Environment.IsEnvironment("IntegrationTests"))
{
    await app.EnsureSchemaVersionAsync();
}

app.Run();

/// <summary>Exposed for WebApplicationFactory in Platform.Services.Tests.</summary>
public partial class Program;
