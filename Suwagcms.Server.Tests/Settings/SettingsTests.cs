using System.Net.Http.Json;
using Suwagcms.Contracts.Settings;
using Suwagcms.Server.Tests.Facts;

namespace Suwagcms.Server.Tests.Settings;

public sealed class SettingsTests(TestDatabase database)
{
    private async Task<int> PageSizeWith(string? value)
    {
        _ = database.DataSource;
        using var app = new TestApp(value);
        var settings = await app.CreateClient().GetFromJsonAsync<ServerSettings>("/api/settings", TestContext.Current.CancellationToken);
        return settings?.PageSize ?? throw new InvalidOperationException("no settings in the answer");
    }

    [Theory]
    [InlineData(null, 20)]
    [InlineData("", 20)]
    [InlineData("5", 5)]
    [InlineData("1", 1)]
    [InlineData("500", 500)]
    [Trait("criterion", "AC-pagination-1")]
    public async Task The_page_size_is_the_variable_or_20_when_it_is_unset_or_empty(string? value, int expected) =>
        Assert.Equal(expected, await PageSizeWith(value));

    [Fact]
    [Trait("criterion", "AC-pagination-1")]
    public async Task A_page_size_of_5_leaves_a_resources_list_route_whole()
    {
        _ = database.DataSource;
        using var app = new TestApp("5");
        var facts = await FactApi.NewBotAsync(app.CreateClient());
        for (var number = 0; number < 25; number++)
        {
            await facts.Add($"customer.field{number}", FactApi.Text("A field"));
        }

        Assert.Equal(25, (await facts.List()).Count);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("2.5")]
    [InlineData("501")]
    [Trait("criterion", "AC-pagination-2")]
    public void A_value_it_cannot_use_stops_the_server_with_a_message_naming_the_variable_the_value_and_the_fix(string value)
    {
        using var app = new TestApp(value);

        var stopped = Assert.ThrowsAny<Exception>(app.CreateClient);

        Assert.Contains("PAGINATION_PAGE_SIZE", stopped.Message, StringComparison.Ordinal);
        Assert.Contains($"'{value}'", stopped.Message, StringComparison.Ordinal);
        Assert.Contains("set a whole number from 1 to 500, or remove it to use 20", stopped.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-pagination-3")]
    public void Compose_passes_the_variable_to_the_server_with_no_default_of_its_own_and_the_example_documents_it()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "compose.yaml")))
        {
            root = root.Parent;
        }
        Assert.NotNull(root);
        var compose = File.ReadAllText(Path.Combine(root.FullName, "compose.yaml")).ReplaceLineEndings("\n");

        var server = compose[
            compose.IndexOf("\n  server:", StringComparison.Ordinal)..compose.IndexOf("\n  web:", StringComparison.Ordinal)
        ];
        Assert.Contains("PAGINATION_PAGE_SIZE: ${PAGINATION_PAGE_SIZE:-}\n", server, StringComparison.Ordinal);
        Assert.Contains("# PAGINATION_PAGE_SIZE=", File.ReadAllText(Path.Combine(root.FullName, ".env.example")), StringComparison.Ordinal);
    }
}
