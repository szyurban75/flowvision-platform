using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Platform.Auth;

namespace Platform.Hosting;

/// <summary>Cookie scheme per interface (Q1-B): signing in to one interface does not sign in to another.</summary>
public static class InterfaceCookies
{
    public static string SchemeFor(string interfaceName) => "FlowVision." + interfaceName;

    public const string RoleClaim = "fv_role";

    /// <summary>Registers one cookie scheme per interface. Every service that checks these cookies needs the shared data-protection keys (see PlatformDefaults).</summary>
    public static AuthenticationBuilder AddInterfaceCookies(this AuthenticationBuilder builder)
    {
        foreach (var name in new[] { Interfaces.Admin, Interfaces.ShopFloor, Interfaces.Documents })
        {
            builder.AddCookie(SchemeFor(name), options =>
            {
                options.Cookie.Name = "FlowVision." + name;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });
        }
        return builder;
    }
}
