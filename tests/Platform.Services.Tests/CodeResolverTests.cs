using ShopFloor.Application;
using Xunit;

namespace Platform.Services.Tests;

/// <summary>T055: a scanned trolley code and a QR code resolve to their order and operation; an unknown code resolves to nothing and changes nothing.</summary>
public class CodeResolverTests
{
    private sealed class FakeLookup(IReadOnlyDictionary<string, CodeTarget> codes) : ICodeLookup
    {
        public int Calls { get; private set; }

        public Task<CodeTarget?> FindAsync(string code, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(codes.TryGetValue(code, out var target) ? target : null);
        }
    }

    private static readonly IReadOnlyDictionary<string, CodeTarget> KnownCodes = new Dictionary<string, CodeTarget>
    {
        ["TRO-1001"] = new CodeTarget(CodeSource.Trolley, RequestId: 501, RequestOperationId: 9001),
        ["QR-2002"] = new CodeTarget(CodeSource.Qr, RequestId: 502, RequestOperationId: 9002),
    };

    [Fact]
    public async Task TrolleyCode_ResolvesToOrderAndOperation()
    {
        var resolver = new CodeResolver(new FakeLookup(KnownCodes));

        var target = await resolver.ResolveAsync("TRO-1001");

        Assert.NotNull(target);
        Assert.Equal(CodeSource.Trolley, target!.Source);
        Assert.Equal(501, target.RequestId);
        Assert.Equal(9001, target.RequestOperationId);
    }

    [Fact]
    public async Task QrCode_ResolvesToOrderAndOperation()
    {
        var resolver = new CodeResolver(new FakeLookup(KnownCodes));

        var target = await resolver.ResolveAsync("QR-2002");

        Assert.NotNull(target);
        Assert.Equal(CodeSource.Qr, target!.Source);
        Assert.Equal(502, target.RequestId);
        Assert.Equal(9002, target.RequestOperationId);
    }

    [Fact]
    public async Task UnknownCode_ResolvesToNothing_AndOnlyReads()
    {
        var lookup = new FakeLookup(KnownCodes);
        var resolver = new CodeResolver(lookup);

        var target = await resolver.ResolveAsync("NOPE-0000");

        Assert.Null(target);
        Assert.Equal(1, lookup.Calls);
    }
}
