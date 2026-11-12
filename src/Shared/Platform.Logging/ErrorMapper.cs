using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Platform.Logging;

/// <summary>Plain-language error for the web interface. Technical details stay in the log only.</summary>
public sealed record ErrorResponse(int Status, string Code, string Message);

/// <summary>Maps exceptions to HTTP responses (T039, ADR-001).</summary>
public static class ErrorMapper
{
    public const string Concurrency = "CONCURRENCY";
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
    public const string Internal = "INTERNAL_ERROR";

    public static ErrorResponse Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            DbUpdateConcurrencyException => new ErrorResponse(
                409, Concurrency,
                "The record was changed by another user. Reload it and try again."),

            SqlException => new ErrorResponse(
                503, ServiceUnavailable,
                "The database is not available right now. Try again in a few minutes."),

            _ => new ErrorResponse(
                500, Internal,
                "An unexpected error occurred. Contact support with the time of the action."),
        };
    }
}
