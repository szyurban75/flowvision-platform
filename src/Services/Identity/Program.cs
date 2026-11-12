using Identity.Api;
using Microsoft.EntityFrameworkCore;
using Platform.Auth;
using Platform.Data;
using Platform.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();

var connectionString = PlatformDefaults.BuildConnectionString(builder.Configuration);
builder.Services.AddDbContext<AuthDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IPasswordVerifier, PasswordVerifier>();
builder.Services.AddScoped<SignInService>();

builder.Services.AddAuthentication().AddInterfaceCookies();
builder.Services.AddAuthorization();

var app = builder.Build();

app.UsePlatformErrorMapping();
app.UseAuthentication();
app.UseAuthorization();

app.MapPlatformHealth();
app.MapSignInEndpoints();

// Refuses to start if PLATFORM_SCHEMA_VERSION does not match Platform:ExpectedSchemaVersion (FR-006).
await app.EnsureSchemaVersionAsync();

app.Run();