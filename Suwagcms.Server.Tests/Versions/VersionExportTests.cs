using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;
using Suwagcms.Server.Tests.Facts;

namespace Suwagcms.Server.Tests.Versions;

public sealed class VersionExportTests(TestDatabase database) : IDisposable
{
    private readonly TestApp app = new();
    private readonly string scratch = Directory.CreateTempSubdirectory("suwagcms-export-server-tests").FullName;

    public void Dispose()
    {
        Directory.Delete(scratch, recursive: true);
        app.Dispose();
    }

    private async Task<(PushScene Scene, TestBot Bot)> FilledAsync()
    {
        _ = database.Pool;
        var client = app.CreateClient();
        var id = PushScene.FreshId();
        var bot = await TestBot.StartAsync(id);
        var scene = await PushScene.RegisterAsync(client, id, bot.Address);
        await scene.FillAsync();
        return (scene, bot);
    }

    private static async Task<VersionExport> ExportOf(PushScene scene, int number) =>
        await scene.Client.GetFromJsonAsync<VersionExport>(
            $"/api/bots/{scene.Bot}/versions/{number}/export",
            TestContext.Current.CancellationToken
        ) ?? throw new InvalidOperationException("no export in the answer");

    [Fact]
    [Trait("criterion", "AC-version-export-1")]
    [Trait("criterion", "AC-version-export-2")]
    public async Task The_route_splits_a_real_version_into_its_files_with_exactly_what_the_document_holds()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await scene.CreateAsync();
        var document = JsonNode.Parse(
            (
                await scene.Client.GetFromJsonAsync<VersionDetail>(
                    $"/api/bots/{scene.Bot}/versions/1",
                    TestContext.Current.CancellationToken
                )
            )?.Document
                ?? ""
        );

        var export = await ExportOf(scene, 1);

        Assert.Equal(
            [
                "articles/cheaper.yaml",
                "articles/priceList.yaml",
                "facts/customer.city.yaml",
                "facts/order.kind.yaml",
                "facts/system.flag.yaml",
                "knowledge-bases/offer.yaml",
                "procedures/conversation.yaml",
                "procedures/orderValuation.yaml",
                "version.yaml",
            ],
            export.Files.Select(file => file.Path).Order(StringComparer.Ordinal)
        );
        foreach (
            var (list, kind) in new[]
            {
                ("facts", ResourceKind.GlobalFact),
                ("knowledgeBases", ResourceKind.KnowledgeBase),
                ("articles", ResourceKind.Article),
                ("procedures", ResourceKind.Procedure),
            }
        )
        {
            var held = document?[list]?.AsArray().Select(item => item?.ToJsonString());
            var written = export
                .Files.Where(file => file.Kind == kind)
                .Select(file => JsonNode.Parse(file.Data.GetRawText())?.ToJsonString());
            Assert.Equal(held, written);
        }
        var head = JsonNode.Parse(export.Files.Single(file => file.Kind is null).Data.GetRawText());
        Assert.Equal(["bot", "number", "formatVersion", "digest"], head?.AsObject().Select(item => item.Key));
        Assert.Equal(document?["digest"]?.GetValue<string>(), head?["digest"]?.GetValue<string>());
    }

    [Fact]
    [Trait("criterion", "AC-version-export-3")]
    [Trait("criterion", "AC-version-export-5")]
    public async Task Changing_or_deleting_exported_files_changes_nothing_and_a_later_export_is_byte_identical()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await scene.CreateAsync();
        var first = Path.Combine(scratch, "first");
        var second = Path.Combine(scratch, "second");
        Assert.Equal(0, (await scene.CliAsync("export", "1", "--out", first)).ExitCode);
        var written = Directory
            .EnumerateFiles(first, "*", SearchOption.AllDirectories)
            .ToDictionary(file => Path.GetRelativePath(first, file), File.ReadAllBytes);
        var before = await scene.SnapshotAsync();

        await File.WriteAllTextAsync(
            Path.Combine(first, "articles", "priceList.yaml"),
            "content: changed",
            TestContext.Current.CancellationToken
        );
        File.Delete(Path.Combine(first, "facts", "customer.city.yaml"));

        Assert.Equal(before, await scene.SnapshotAsync());
        Assert.Equal(0, (await scene.CliAsync("export", "1", "--out", second)).ExitCode);
        var again = Directory
            .EnumerateFiles(second, "*", SearchOption.AllDirectories)
            .ToDictionary(file => Path.GetRelativePath(second, file), File.ReadAllBytes);
        Assert.Equal(written.Keys.Order(StringComparer.Ordinal), again.Keys.Order(StringComparer.Ordinal));
        Assert.All(written, file => Assert.Equal(file.Value, again[file.Key]));
    }

    [Fact]
    [Trait("criterion", "AC-version-export-6")]
    public async Task A_number_the_bot_does_not_have_and_a_bot_with_no_version_are_refused_with_exit_code_1()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;

        var none = await scene.CliAsync("export", "--out", Path.Combine(scratch, "none"));
        Assert.Equal(1, none.ExitCode);
        Assert.Contains("has no version yet", none.Error, StringComparison.Ordinal);

        await scene.CreateAsync();
        var response = await scene.Client.GetAsync($"/api/bots/{scene.Bot}/versions/9/export", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(["version.missing"], await FactApi.CodesOf(response));
        var missing = await scene.CliAsync("export", "9", "--out", Path.Combine(scratch, "missing"));
        Assert.Equal(1, missing.ExitCode);
        Assert.Contains("has no version 9; its latest is 1.", missing.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(scratch, "missing")));
    }

    [Fact]
    [Trait("criterion", "AC-version-export-9")]
    public async Task Exporting_changes_nothing_in_the_cms_and_sends_no_request_to_the_bot()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await scene.CreateAsync();
        var before = await scene.SnapshotAsync();
        var requests = bot.Requests;

        Assert.Equal(0, (await scene.CliAsync("export", "--out", Path.Combine(scratch, "latest"))).ExitCode);

        Assert.Equal(before, await scene.SnapshotAsync());
        Assert.Equal(requests, bot.Requests);
    }
}
