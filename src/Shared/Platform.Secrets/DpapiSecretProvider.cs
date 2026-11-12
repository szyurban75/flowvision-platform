using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Platform.Secrets;

/// <summary>
/// Reads a password protected with DPAPI at LocalMachine scope (ADR-001, secondary option when no gMSA is available).
/// The value is never logged and never included in exception messages.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiSecretProvider : ISecretProvider
{
    private readonly string _filePath;

    public DpapiSecretProvider(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    /// <summary>Default location: %ProgramData%\FlowVision\secrets\&lt;name&gt;.bin</summary>
    public static string DefaultPath(string name) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FlowVision", "secrets", name + ".bin");

    public string GetSqlPassword()
    {
        if (!File.Exists(_filePath))
        {
            throw new FileNotFoundException("Protected secret file is missing.", _filePath);
        }

        var protectedBytes = File.ReadAllBytes(_filePath);
        var clear = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.LocalMachine);
        try
        {
            return Encoding.UTF8.GetString(clear);
        }
        finally
        {
            Array.Clear(clear);
        }
    }

    /// <summary>Encrypts a password at LocalMachine scope. Used by tests and the deployment script.</summary>
    public static byte[] Protect(string password)
    {
        var clear = Encoding.UTF8.GetBytes(password);
        try
        {
            return ProtectedData.Protect(clear, optionalEntropy: null, DataProtectionScope.LocalMachine);
        }
        finally
        {
            Array.Clear(clear);
        }
    }
}
