namespace Platform.Data;

/// <summary>Parses the row version sent by a web client in the If-Match header.</summary>
public static class RowVersionExtensions
{
    /// <summary>SQL Server rowversion (timestamp) is always 8 bytes.</summary>
    public const int RowVersionLength = 8;

    /// <summary>
    /// Accepts a quoted base64 value, for example <c>"AAAAAAAAB9E="</c>, as sent by the web interface from the ETag.
    /// Returns false for a missing, malformed, or wrong-length value. Never throws.
    /// </summary>
    public static bool TryParseIfMatch(string? headerValue, out byte[]? rowVersion)
    {
        rowVersion = null;
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        var text = headerValue.Trim();
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
        {
            text = text[1..^1];
        }

        if (text.Length == 0)
        {
            return false;
        }

        Span<byte> buffer = stackalloc byte[RowVersionLength];
        if (!Convert.TryFromBase64String(text, buffer, out var written) || written != RowVersionLength)
        {
            return false;
        }

        rowVersion = buffer.ToArray();
        return true;
    }

    /// <summary>Formats a row version as the ETag value the web interface sends back.</summary>
    public static string ToETag(byte[] rowVersion) => "\"" + Convert.ToBase64String(rowVersion) + "\"";
}
