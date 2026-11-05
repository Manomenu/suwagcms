using System.Net;
using System.Text;
using System.Text.Json;

namespace Suwagcms.Cli.Tests.Bots;

public sealed class BotsTests
{
    private const string TwoBots = """
        [{"id":"rzeczoznawca","name":"Rzeczoznawca","address":"http://rzeczoznawca.internal:8080"},
         {"id":"sklep","name":"Sklep","address":"http://sklep.internal:8080"}]
        """;

    private static StubServer Listing(string json = TwoBots) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

    private static string Normalized(string text) =>
        string.Join("\n", text.ReplaceLineEndings("\n").Split('\n').Select(line => line.TrimEnd()));

    [Fact]
    [Trait("criterion", "AC-bot-profiles-1")]
    public async Task Bots_list_prints_every_registered_bot_with_its_name_and_address_and_marks_the_one_in_use()
    {
        var result = await TestCli.Run(
            ["bots", "list", "--server", "http://cms.internal"],
            Listing(),
            new() { ["SUWAGCMS_BOT"] = "sklep" }
        );

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            "  rzeczoznawca  Rzeczoznawca  http://rzeczoznawca.internal:8080\n* sklep         Sklep         http://sklep.internal:8080\n",
            Normalized(result.Output)
        );
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-1")]
    public async Task Bots_list_with_json_prints_the_bots_and_the_active_one()
    {
        var server = Listing();

        var result = await TestCli.Run(["bots", "list", "--json", "--server", "http://cms.internal"], server);

        using var document = JsonDocument.Parse(result.Output);
        Assert.Equal("http://cms.internal/api/bots", Assert.Single(server.Requests).ToString());
        Assert.Equal(
            ["rzeczoznawca", "sklep"],
            document.RootElement.GetProperty("bots").EnumerateArray().Select(bot => bot.GetProperty("id").GetString())
        );
        Assert.False(document.RootElement.TryGetProperty("active", out _));
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-7")]
    public async Task Bots_list_with_no_bot_registered_says_how_to_register_one()
    {
        var result = await TestCli.Run(["bots", "list", "--server", "http://cms.internal"], Listing("[]"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("suwagcms bots add", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-3")]
    public async Task Bots_add_registers_the_bot_with_what_was_typed_and_json_returns_its_id_name_and_address()
    {
        string? sent = null;
        var server = new StubServer(request =>
        {
            using var reader = new StreamReader(request.Content?.ReadAsStream() ?? Stream.Null);
            sent = request.Method + " " + reader.ReadToEnd();
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(
                    """{"id":"sklep","name":"Sklep","address":"http://sklep.internal:8080"}""",
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        });

        var result = await TestCli.Run(
            [
                "bots",
                "add",
                "sklep",
                "--name",
                "Sklep",
                "--address",
                "http://sklep.internal:8080",
                "--json",
                "--server",
                "http://cms.internal",
            ],
            server
        );

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("http://cms.internal/api/bots", Assert.Single(server.Requests).ToString());
        Assert.Equal("""POST {"id":"sklep","name":"Sklep","address":"http://sklep.internal:8080"}""", sent);
        using var document = JsonDocument.Parse(result.Output);
        Assert.Equal("sklep", document.RootElement.GetProperty("id").GetString());
        Assert.Equal("Sklep", document.RootElement.GetProperty("name").GetString());
        Assert.Equal("http://sklep.internal:8080", document.RootElement.GetProperty("address").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-4")]
    public async Task Bots_add_with_nothing_given_still_asks_the_server_and_prints_every_mistake_with_its_fix_and_exits_1()
    {
        var refusal = StubServer.Answering(
            HttpStatusCode.UnprocessableEntity,
            """{"mistakes":[{"code":"bot.idInvalid","path":"id","message":"The id is missing.","fix":"Send an id."},{"code":"bot.nameMissing","path":"name","message":"The name is blank.","fix":"Send a name."}]}"""
        );

        var result = await TestCli.Run(["bots", "add", "--server", "http://cms.internal"], refusal);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Send an id.", result.Error, StringComparison.Ordinal);
        Assert.Contains("Send a name.", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-5")]
    public async Task Bots_add_for_a_taken_id_prints_the_mistake_naming_it_and_exits_1()
    {
        var taken = StubServer.Answering(
            HttpStatusCode.Conflict,
            """{"mistakes":[{"code":"bot.idTaken","path":"id","message":"A bot with the id 'sklep' already exists.","fix":"Choose another id."}]}"""
        );

        var result = await TestCli.Run(
            ["bots", "add", "sklep", "--name", "S", "--address", "http://s", "--server", "http://cms.internal"],
            taken
        );

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("'sklep'", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-9")]
    public async Task The_bot_option_wins_over_the_variable_and_the_stored_bot()
    {
        var folder = TestCli.FreshSettingsFolder();
        await TestCli.Run(["use", "sklep", "--server", "http://cms.internal"], Listing(), settingsFolder: folder);

        var result = await TestCli.Run(
            ["use", "--bot", "rzeczoznawca", "--json", "--server", "http://cms.internal"],
            Listing(),
            new() { ["SUWAGCMS_BOT"] = "sklep" },
            folder
        );

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("rzeczoznawca", JsonDocument.Parse(result.Output).RootElement.GetProperty("bot").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-9")]
    public async Task The_variable_wins_over_the_stored_bot()
    {
        var folder = TestCli.FreshSettingsFolder();
        await TestCli.Run(["use", "sklep", "--server", "http://cms.internal"], Listing(), settingsFolder: folder);

        var result = await TestCli.Run(
            ["use", "--json", "--server", "http://cms.internal"],
            Listing(),
            new() { ["SUWAGCMS_BOT"] = "rzeczoznawca" },
            folder
        );

        Assert.Equal("rzeczoznawca", JsonDocument.Parse(result.Output).RootElement.GetProperty("bot").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-9")]
    public async Task The_stored_bot_is_used_when_neither_the_option_nor_the_variable_is_set()
    {
        var folder = TestCli.FreshSettingsFolder();
        var stored = await TestCli.Run(["use", "sklep", "--server", "http://cms.internal"], Listing(), settingsFolder: folder);

        var result = await TestCli.Run(["bots", "list", "--server", "http://cms.internal"], Listing(), settingsFolder: folder);

        Assert.Equal(0, stored.ExitCode);
        Assert.Contains("* sklep", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("* rzeczoznawca", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-10")]
    public async Task A_command_that_needs_a_bot_with_none_chosen_exits_2_and_says_how_to_choose_one_and_lists_the_bots()
    {
        var result = await TestCli.Run(["use", "--server", "http://cms.internal"], Listing());

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("--bot", result.Error, StringComparison.Ordinal);
        Assert.Contains("SUWAGCMS_BOT", result.Error, StringComparison.Ordinal);
        Assert.Contains("suwagcms use", result.Error, StringComparison.Ordinal);
        Assert.Contains("rzeczoznawca, sklep", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-10")]
    public async Task A_bot_that_does_not_exist_exits_1_and_lists_the_bots_that_do()
    {
        var result = await TestCli.Run(["use", "--bot", "rzeczoznawka", "--server", "http://cms.internal"], Listing());

        Assert.Equal(1, result.ExitCode);
        Assert.Equal("No bot 'rzeczoznawka'. Bots: rzeczoznawca, sklep." + Environment.NewLine, result.Error);
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-10")]
    public async Task Using_a_bot_that_does_not_exist_stores_nothing()
    {
        var folder = TestCli.FreshSettingsFolder();

        var result = await TestCli.Run(["use", "nope", "--server", "http://cms.internal"], Listing(), settingsFolder: folder);

        Assert.Equal(1, result.ExitCode);
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-12")]
    public async Task Using_a_bot_without_a_settings_folder_is_refused_and_writes_nothing_in_the_current_directory()
    {
        var before = Directory.GetFileSystemEntries(Directory.GetCurrentDirectory());

        var result = await TestCli.Run(["use", "sklep", "--server", "http://cms.internal"], Listing(), settingsFolder: "");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("HOME", result.Error, StringComparison.Ordinal);
        Assert.Equal(before, Directory.GetFileSystemEntries(Directory.GetCurrentDirectory()));
    }
}
