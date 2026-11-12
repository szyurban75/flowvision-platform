using Platform.SchemaCheck;
using Xunit;

namespace Platform.Services.Tests;

public class SchemaVersionCheckerTests
{
    [Fact]
    public void MatchingVersion_IsCompatible()
    {
        var result = SchemaVersionChecker.Evaluate("2.0.36", "2.0.36");

        Assert.Equal(SchemaStatus.Match, result.Status);
        Assert.True(result.IsCompatible);
    }

    [Fact]
    public void OlderDatabase_IsRefused()
    {
        var result = SchemaVersionChecker.Evaluate("2.0.36", "2.0.35");

        Assert.Equal(SchemaStatus.DatabaseOlder, result.Status);
        Assert.False(result.IsCompatible);
    }

    [Fact]
    public void NewerDatabase_IsRefused()
    {
        var result = SchemaVersionChecker.Evaluate("2.0.36", "2.0.37");

        Assert.Equal(SchemaStatus.DatabaseNewer, result.Status);
        Assert.False(result.IsCompatible);
    }

    [Fact]
    public void NumericComparison_NotText()
    {
        // "2.0.9" < "2.0.36" numerically, although "2.0.9" > "2.0.36" as text.
        Assert.Equal(SchemaStatus.DatabaseOlder, SchemaVersionChecker.Evaluate("2.0.36", "2.0.9").Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-version")]
    public void MissingOrInvalidVersion_IsUnknown(string? found)
    {
        var result = SchemaVersionChecker.Evaluate("2.0.36", found);

        Assert.Equal(SchemaStatus.Unknown, result.Status);
        Assert.False(result.IsCompatible);
    }
}
