namespace Platform.Secrets;

/// <summary>Supplies the SQL login password for a service. Returns null when Windows Integrated Security is used.</summary>
public interface ISecretProvider
{
    string? GetSqlPassword();
}
