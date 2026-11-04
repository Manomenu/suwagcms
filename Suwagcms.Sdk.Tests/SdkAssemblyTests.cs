using System.Reflection;

namespace Suwagcms.Sdk.Tests;

public sealed class SdkAssemblyTests
{
    [Fact]
    public void TheSdkAssemblyLoadsWithTheLocalVersion()
    {
        var sdk = Assembly.Load("Suwagcms.Sdk");

        Assert.StartsWith(
            "0.0.0-local",
            sdk.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            StringComparison.Ordinal
        );
    }
}
