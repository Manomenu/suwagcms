using System.Xml.Linq;
using Suwagcms.Sdk.Tests.Repository;

namespace Suwagcms.Sdk.Tests.Package;

public sealed class PackageTests
{
    private static readonly XDocument Project = XDocument.Load(RepositoryFiles.Path("Suwagcms.Sdk/Suwagcms.Sdk.csproj"));

    [Fact]
    [Trait("criterion", "AC-sdk-validator-12")]
    public void The_sdk_packs_as_Suwagcms_Sdk()
    {
        Assert.Equal("Suwagcms.Sdk", Property("PackageId"));
        Assert.NotEqual("false", Property("IsPackable"));
    }

    [Fact]
    [Trait("criterion", "AC-sdk-validator-11")]
    public void The_package_carries_the_schema()
    {
        Assert.Contains(
            Project.Descendants("None"),
            item => item.Attribute("Include")?.Value == "definition-format.schema.json" && item.Attribute("Pack")?.Value == "true"
        );
    }

    private static string? Property(string name) => Project.Descendants(name).Select(element => element.Value).SingleOrDefault();
}
