using Platform.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();

var app = builder.Build();

app.UsePlatformErrorMapping();
app.MapPlatformHealth();

// Refuses to start if PLATFORM_SCHEMA_VERSION does not match Platform:ExpectedSchemaVersion (FR-006).
await app.EnsureSchemaVersionAsync();

app.Run();