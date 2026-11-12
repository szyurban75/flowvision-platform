using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Platform.Logging;
using Xunit;

namespace Platform.Services.Tests;

public class ErrorMapperTests
{
    [Fact]
    public void ConcurrencyException_Maps_To409_CONCURRENCY()
    {
        var exception = new DbUpdateConcurrencyException("stale", new InvalidOperationException("inner"));

        var mapped = ErrorMapper.Map(exception);

        Assert.Equal(409, mapped.Status);
        Assert.Equal("CONCURRENCY", mapped.Code);
    }

    [Fact]
    public void SqlException_Maps_To503_SERVICE_UNAVAILABLE()
    {
        // Connecting to a closed local port raises a real SqlException without needing a database.
        var exception = Assert.ThrowsAny<SqlException>(() =>
        {
            using var connection = new SqlConnection("Server=tcp:127.0.0.1,1;Connect Timeout=1;Encrypt=False;Integrated Security=True");
            connection.Open();
        });

        var mapped = ErrorMapper.Map(exception);

        Assert.Equal(503, mapped.Status);
        Assert.Equal("SERVICE_UNAVAILABLE", mapped.Code);
    }

    [Fact]
    public void OtherException_Maps_To500_WithoutTechnicalDetails()
    {
        var exception = new InvalidOperationException("secret internal detail: connection string Password=abc");

        var mapped = ErrorMapper.Map(exception);

        Assert.Equal(500, mapped.Status);
        Assert.Equal("INTERNAL_ERROR", mapped.Code);
        Assert.DoesNotContain("secret", mapped.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", mapped.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" at ", mapped.Message);
    }
}
