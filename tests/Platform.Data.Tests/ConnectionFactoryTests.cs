using Platform.Secrets;
using Xunit;

namespace Platform.Data.Tests;

public class ConnectionFactoryTests
{
    private sealed class FixedSecret(string? value) : ISecretProvider
    {
        public string? GetSqlPassword() => value;
    }

    [Fact]
    public void GmsaMode_UsesIntegratedSecurity_AndNoPassword()
    {
        var options = new DatabaseOptions { Server = "localhost\\FI", Database = "FLOWVISION2_TEST", AuthMode = "Gmsa" };

        var connection = ConnectionFactory.Build(options, new GmsaConnectionProvider());

        Assert.Contains("Integrated Security=True", connection);
        Assert.DoesNotContain("Password", connection, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Initial Catalog=FLOWVISION2_TEST", connection);
    }

    [Fact]
    public void DpapiMode_UsesSqlLogin_WithProtectedPassword()
    {
        var options = new DatabaseOptions { Server = "localhost\\FI", Database = "FLOWVISION2_TEST", AuthMode = "Dpapi", SqlUser = "fv_app" };

        var connection = ConnectionFactory.Build(options, new FixedSecret("s3cret-value"));

        Assert.Contains("User ID=fv_app", connection);
        Assert.Contains("Password=s3cret-value", connection);
    }

    [Fact]
    public void DpapiMode_WithoutSqlUser_Fails()
    {
        var options = new DatabaseOptions { AuthMode = "Dpapi", SqlUser = null };

        Assert.Throws<InvalidOperationException>(() => ConnectionFactory.Build(options, new FixedSecret("x")));
    }

    [Fact]
    public void UnknownAuthMode_Fails()
    {
        var options = new DatabaseOptions { AuthMode = "Plain" };

        Assert.Throws<InvalidOperationException>(() => ConnectionFactory.Build(options, new GmsaConnectionProvider()));
    }
}
