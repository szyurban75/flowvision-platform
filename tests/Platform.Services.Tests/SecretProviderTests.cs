using System.Runtime.Versioning;
using Platform.Data;
using Platform.Secrets;
using Xunit;

namespace Platform.Services.Tests;

[SupportedOSPlatform("windows")]
public class SecretProviderTests
{
    [SkippableFact]
    public void Dpapi_RoundTrip_ReturnsOriginalPassword()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "DPAPI is available on Windows only.");

        var file = Path.Combine(Path.GetTempPath(), "fv-secret-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            File.WriteAllBytes(file, DpapiSecretProvider.Protect("pilot-password-42"));

            var provider = new DpapiSecretProvider(file);

            Assert.Equal("pilot-password-42", provider.GetSqlPassword());
        }
        finally
        {
            File.Delete(file);
        }
    }

    [SkippableFact]
    public void Dpapi_StoredFileIsNotPlainText()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "DPAPI is available on Windows only.");

        var protectedBytes = DpapiSecretProvider.Protect("pilot-password-42");

        Assert.DoesNotContain("pilot-password-42", System.Text.Encoding.UTF8.GetString(protectedBytes));
    }

    [Fact]
    public void Gmsa_ReturnsNoPassword()
    {
        var provider = new GmsaConnectionProvider();

        Assert.Null(provider.GetSqlPassword());
    }

    [SkippableFact]
    public void Dpapi_MissingFile_ErrorDoesNotContainSecret()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "DPAPI is available on Windows only.");

        var provider = new DpapiSecretProvider(Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N") + ".bin"));

        var exception = Assert.Throws<FileNotFoundException>(() => provider.GetSqlPassword());

        Assert.DoesNotContain("pilot-password-42", exception.Message);
    }
}
