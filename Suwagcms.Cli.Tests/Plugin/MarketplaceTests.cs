using System.Text.Json;

namespace Suwagcms.Cli.Tests.Plugin;

public sealed class MarketplaceTests
{
    private static JsonElement Read(string relativePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot.Path, relativePath)));
        return document.RootElement.Clone();
    }

    [Fact]
    [Trait("criterion", "AC-claude-plugin-1")]
    public void The_marketplace_offers_one_plugin_named_suwagcms()
    {
        var marketplace = Read(".claude-plugin/marketplace.json");

        Assert.Equal("suwagcms", marketplace.GetProperty("name").GetString());
        var plugin = Assert.Single(marketplace.GetProperty("plugins").EnumerateArray());
        Assert.Equal("suwagcms", plugin.GetProperty("name").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-claude-plugin-1")]
    public void The_marketplace_entry_points_at_the_folder_whose_manifest_names_the_plugin_suwagcms()
    {
        var marketplace = Read(".claude-plugin/marketplace.json");
        var source = marketplace.GetProperty("plugins")[0].GetProperty("source").GetString();

        Assert.NotNull(source);
        var manifest = Read(Path.Combine(source, ".claude-plugin", "plugin.json"));
        Assert.Equal("suwagcms", manifest.GetProperty("name").GetString());
        Assert.False(string.IsNullOrWhiteSpace(manifest.GetProperty("description").GetString()));
    }
}
