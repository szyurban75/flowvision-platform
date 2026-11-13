extern alias AdminHost;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using AdminProgram = AdminHost::Program;
using AdminHost::Admin.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Platform.Auth;
using Platform.Data;
using Platform.Hosting;
using Xunit;

namespace Platform.Services.Tests;

/// <summary>
/// Admin host with the real endpoint pipeline. SQL Server is replaced by EF InMemory and the startup schema check is skipped.
/// Each instance gets its own database and key folder.
/// </summary>
public sealed class AdminHostFactory : WebApplicationFactory<AdminProgram>
{
    private static readonly ServiceProvider InMemoryEngine = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();
    private readonly string _databaseName = Guid.NewGuid().ToString("N");
    private readonly string _keysPath = Path.Combine(Path.GetTempPath(), "fv-admin-test-keys-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Platform:DataProtectionKeysPath"] = _keysPath,
        }));
        builder.ConfigureTestServices(services =>
        {
            // Program registers the SQL Server options first; remove them so only the in-memory provider applies.
            services.RemoveAll<DbContextOptions<AuthDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AuthDbContext>>();
            services.AddDbContext<AuthDbContext>(options => options
                .UseInMemoryDatabase(_databaseName)
                .UseInternalServiceProvider(InMemoryEngine));
        });
    }

    public void SeedUsers(params UserAccount[] users)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        db.Users.AddRange(users);
        db.SaveChanges();
    }

    public UserAccount? LoadUser(int id)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        return db.Users.AsNoTracking().FirstOrDefault(u => u.USERS_ID == id);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_keysPath))
        {
            Directory.Delete(_keysPath, recursive: true);
        }
    }
}

/// <summary>T047 over HTTP: auth, precondition, lookup, validation and the stored hash of POST /api/admin/users/{id}/password-reset.</summary>
public sealed class AdminResetEndpointTests
{
    private const string TargetUrl = "/api/admin/users/1001/password-reset";
    private const string MissingUrl = "/api/admin/users/999999/password-reset";
    private const string GoodPassword = "NewSecret123";
    private static readonly byte[] CurrentRowVersion = [0, 0, 0, 0, 0, 0, 0x33, 0x34];

    private static UserAccount Target(string? password = "1234567890") => new()
    {
        USERS_ID = 1001,
        USERS_USERNAME = "otokarczuk",
        USERS_NAME = "O. Tokarczuk",
        USERS_ROLE = "U",
        USERS_PASSWORD = password,
        ROWVER = (byte[])CurrentRowVersion.Clone(),
    };

    private static HttpClient Client(AdminHostFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false,
        AllowAutoRedirect = false,
    });

    private static string IfMatch(byte[] rowVersion) => $"\"{Convert.ToBase64String(rowVersion)}\"";

    /// <summary>A cookie for the admin scheme, protected with the host's own key ring, as the sign-in endpoint would issue it.</summary>
    private static string AdminCookie(AdminHostFactory factory, string role)
    {
        var scheme = InterfaceCookies.SchemeFor(Interfaces.Admin);
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(scheme);
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "1000"),
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(InterfaceCookies.RoleClaim, role),
            ],
            scheme);
        var properties = new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            IsPersistent = true,
            AllowRefresh = true,
        };
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), properties, scheme);
        return $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}";
    }

    private static HttpRequestMessage ResetRequest(string url, string password, string? ifMatch, string? cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new PasswordResetRequest(password)),
        };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }
        if (cookie is not null)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }
        return request;
    }

    [Fact]
    public async Task NoCookie_Returns401()
    {
        using var factory = new AdminHostFactory();
        factory.SeedUsers(Target());
        using var client = Client(factory);

        using var response = await client.SendAsync(ResetRequest(TargetUrl, GoodPassword, IfMatch(CurrentRowVersion), cookie: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OperatorCookie_OnAdminInterface_Returns403()
    {
        using var factory = new AdminHostFactory();
        factory.SeedUsers(Target());
        using var client = Client(factory);

        using var response = await client.SendAsync(ResetRequest(TargetUrl, GoodPassword, IfMatch(CurrentRowVersion), AdminCookie(factory, role: "U")));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MissingIfMatch_Returns428()
    {
        using var factory = new AdminHostFactory();
        factory.SeedUsers(Target());
        using var client = Client(factory);

        using var response = await client.SendAsync(ResetRequest(TargetUrl, GoodPassword, ifMatch: null, AdminCookie(factory, role: "A")));

        Assert.Equal((HttpStatusCode)428, response.StatusCode);
    }

    [Fact]
    public async Task ShortPassword_Returns400_WeakPassword()
    {
        using var factory = new AdminHostFactory();
        factory.SeedUsers(Target());
        using var client = Client(factory);

        using var response = await client.SendAsync(ResetRequest(TargetUrl, "short", IfMatch(CurrentRowVersion), AdminCookie(factory, role: "A")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("WEAK_PASSWORD", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnknownUser_Returns404()
    {
        using var factory = new AdminHostFactory();
        factory.SeedUsers(Target());
        using var client = Client(factory);

        using var response = await client.SendAsync(ResetRequest(MissingUrl, GoodPassword, IfMatch(CurrentRowVersion), AdminCookie(factory, role: "A")));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("NOT_FOUND", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StaleIfMatch_Returns409()
    {
        using var factory = new AdminHostFactory();
        factory.SeedUsers(Target());
        using var client = Client(factory);
        var stale = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0x01 };

        using var response = await client.SendAsync(ResetRequest(TargetUrl, GoodPassword, IfMatch(stale), AdminCookie(factory, role: "A")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("CONCURRENCY", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ValidReset_Returns204_AndStoresVerifiableHash()
    {
        using var factory = new AdminHostFactory();
        factory.SeedUsers(Target());
        using var client = Client(factory);

        using var response = await client.SendAsync(ResetRequest(TargetUrl, GoodPassword, IfMatch(CurrentRowVersion), AdminCookie(factory, role: "A")));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var stored = factory.LoadUser(1001);
        Assert.NotNull(stored);
        Assert.StartsWith("AQAAAA", stored!.USERS_PASSWORD, StringComparison.Ordinal);
        Assert.Equal(PasswordCheck.Valid, new PasswordVerifier().Verify(stored, GoodPassword));
        Assert.Equal(PasswordCheck.Invalid, new PasswordVerifier().Verify(stored, "wrong-password"));
    }
}
