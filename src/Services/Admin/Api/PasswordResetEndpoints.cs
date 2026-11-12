using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Auth;
using Platform.Data;
using Platform.Hosting;

namespace Admin.Api;

public sealed record PasswordResetRequest(string NewPassword);

/// <summary>
/// POST /api/admin/users/{id}/password-reset (T047). Sets a new PBKDF2 hash for a user, which also clears a legacy value (OQ-005).
/// Only an administrator signed in to the admin interface may call it. The caller sends the row version from the user record
/// in If-Match, so a reset that races another edit is refused with 409 instead of overwriting it.
/// </summary>
public static class PasswordResetEndpoints
{
    public const string PolicyName = "AdminOnly";
    public const int MinimumPasswordLength = 8;

    public static IEndpointRouteBuilder MapPasswordResetEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/users/{id:int}/password-reset", async (
            int id,
            PasswordResetRequest request,
            HttpContext http,
            AuthDbContext db,
            IPasswordVerifier passwords,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < MinimumPasswordLength)
            {
                return Results.BadRequest(new { code = "WEAK_PASSWORD", message = $"The password must be at least {MinimumPasswordLength} characters." });
            }

            if (!RowVersionExtensions.TryParseIfMatch(http.Request.Headers["If-Match"].ToString(), out var rowVersion) || rowVersion is null)
            {
                return Results.Json(new { code = "PRECONDITION_REQUIRED", message = "Send the row version of the user record in If-Match." }, statusCode: 428);
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.USERS_ID == id, cancellationToken);
            if (user is null)
            {
                return Results.NotFound(new { code = "NOT_FOUND", message = "User not found." });
            }

            db.Entry(user).Property(u => u.ROWVER).OriginalValue = rowVersion;
            user.USERS_PASSWORD = passwords.HashPassword(user, request.NewPassword);
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization(PolicyName);

        return app;
    }
}
