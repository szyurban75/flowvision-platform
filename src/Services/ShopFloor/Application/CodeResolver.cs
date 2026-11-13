namespace ShopFloor.Application;

/// <summary>Whether a scanned code is a trolley code or a QR code.</summary>
public enum CodeSource
{
    Trolley,
    Qr,
}

/// <summary>The order (request) and operation a scanned code points to.</summary>
public sealed record CodeTarget(CodeSource Source, int RequestId, int RequestOperationId);

/// <summary>Read-only lookup of a scanned code. Returns null when no trolley or QR code matches.</summary>
public interface ICodeLookup
{
    Task<CodeTarget?> FindAsync(string code, CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves a scanned code to an order and operation (T064). Returns null for an unknown or empty code, which the endpoint reports as UNKNOWN_CODE.
/// Read-only: it never writes, so an unknown code cannot change a record.
/// </summary>
public sealed class CodeResolver(ICodeLookup lookup)
{
    private readonly ICodeLookup _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));

    public async Task<CodeTarget?> ResolveAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);

        var trimmed = code.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        return await _lookup.FindAsync(trimmed, cancellationToken);
    }
}
