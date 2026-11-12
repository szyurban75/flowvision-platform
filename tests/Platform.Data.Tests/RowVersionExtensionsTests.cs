using Xunit;

namespace Platform.Data.Tests;

public class RowVersionExtensionsTests
{
    [Fact]
    public void ParsesQuotedBase64Etag()
    {
        var original = new byte[] { 0, 0, 0, 0, 0, 0, 7, 209 };
        var etag = RowVersionExtensions.ToETag(original);

        Assert.True(RowVersionExtensions.TryParseIfMatch(etag, out var parsed));
        Assert.Equal(original, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    [InlineData("not base64!")]
    [InlineData("\"AAAA\"")]          // 3 bytes, too short
    [InlineData("\"AAAAAAAAAAAAAAAAAA==\"")] // too long
    public void RejectsMalformedValues(string? header)
    {
        Assert.False(RowVersionExtensions.TryParseIfMatch(header, out var parsed));
        Assert.Null(parsed);
    }
}
