namespace Platform.Secrets;

/// <summary>
/// Primary option (ADR-001): the service runs under a group Managed Service Account and connects with
/// Windows Integrated Security, so no password is stored anywhere.
/// </summary>
public sealed class GmsaConnectionProvider : ISecretProvider
{
    public string? GetSqlPassword() => null;
}
