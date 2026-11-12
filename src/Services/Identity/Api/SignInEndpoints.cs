using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Platform.Auth;

namespace Identity.Api;

public sealed record SignInRequest(string Username, string Password, string Interface);

/// <summary>Cookie scheme per interface (Q1-B): signing in to one interface does not sign in to another.</summary>
public static class InterfaceCookies
{
    public static string SchemeFor(string interfaceName) => "FlowVision." + interfaceName;

    public const string RoleClaim = "fv_role";
}

/// <summary>POST /api/identity/sign-in, POST /api/identity/sign-out, GET /api/identity/me (T048).</summary>
public static class SignInEndpoints
{
    public static IEndpointRouteBuilder MapSignInEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/identity");

        group.MapPost("/sign-in", async (SignInRequest request, SignInService signIn, HttpContext http, CancellationToken cancellationToken) =>
        {
            var result = await signIn.SignInAsync(request.Username, request.Password, request.Interface, cancellationToken);
            switch (result.Status)
            {
                case SignInStatus.Succeeded:
                    var user = result.User!;
                    var claims = new List<Claim>
                    {
                        new(ClaimTypes.NameIdentifier, user.USERS_ID.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        new(ClaimTypes.Name, user.USERS_USERNAME),
                        new(InterfaceCookies.RoleClaim, user.USERS_ROLE.Trim()),
                    };
                    var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, InterfaceCookies.SchemeFor(request.Interface)));
                    await http.SignInAsync(InterfaceCookies.SchemeFor(request.Interface), principal);
                    return Results.Ok(new { status = "ok", user = new { id = user.USERS_ID, name = user.USERS_USERNAME, role = user.USERS_ROLE.Trim() } });

                case SignInStatus.InvalidCredentials:
                    return Results.Json(new { code = "INVALID_CREDENTIALS", message = "Wrong user name or password." }, statusCode: 401);

                case SignInStatus.PasswordResetRequired:
                    return Results.Json(new { code = "PASSWORD_RESET_REQUIRED", message = "Your password must be reset by an administrator." }, statusCode: 403);

                case SignInStatus.InterfaceNotAllowed:
                    return Results.Json(new { code = "INTERFACE_NOT_ALLOWED", message = "Your account has no access to this interface." }, statusCode: 403);

                default:
                    return Results.BadRequest(new { code = "UNKNOWN_INTERFACE", message = "Unknown interface." });
            }
        });

        group.MapPost("/sign-out", async (string @interface, HttpContext http) =>
        {
            if (!Interfaces.IsKnown(@interface))
            {
                return Results.BadRequest(new { code = "UNKNOWN_INTERFACE", message = "Unknown interface." });
            }
            await http.SignOutAsync(InterfaceCookies.SchemeFor(@interface));
            return Results.NoContent();
        });

        group.MapGet("/me", async (string @interface, HttpContext http) =>
        {
            if (!Interfaces.IsKnown(@interface))
            {
                return Results.BadRequest(new { code = "UNKNOWN_INTERFACE", message = "Unknown interface." });
            }

            var auth = await http.AuthenticateAsync(InterfaceCookies.SchemeFor(@interface));
            if (!auth.Succeeded || auth.Principal is null)
            {
                return Results.Json(new { code = "NOT_SIGNED_IN", message = "Sign in first." }, statusCode: 401);
            }

            return Results.Ok(new
            {
                id = auth.Principal.FindFirstValue(ClaimTypes.NameIdentifier),
                name = auth.Principal.FindFirstValue(ClaimTypes.Name),
                role = auth.Principal.FindFirstValue(InterfaceCookies.RoleClaim),
            });
        });

        return app;
    }

    /// <summary>Registers one cookie scheme per interface.</summary>
    public static AuthenticationBuilder AddInterfaceCookies(this AuthenticationBuilder builder)
    {
        foreach (var name in new[] { Interfaces.Admin, Interfaces.ShopFloor, Interfaces.Documents })
        {
            builder.AddCookie(InterfaceCookies.SchemeFor(name), options =>
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
