using System.Net;
using System.Text;

namespace Suwagcms.Cli.Tests.Output;

public sealed class OpenSavedTests : IDisposable
{
    private const string Fact = """{"name":"one","type":"text","description":"d","writableBy":"set","maxLength":80,"revision":3}""";
    private const string Base = """{"id":"one","title":"T","language":"pl","revision":3}""";
    private const string Article =
        """{"id":"one","knowledgeBase":"offer","language":"pl","title":"T","content":"c","llmGuide":"g","description":"d","revision":3}""";
    private const string Action =
        """{"id":"one","description":"d","parameters":{},"results":{},"implementation":{"http":{"method":"GET","url":"https://a.b/c","headers":{},"timeoutSeconds":20}},"revision":3,"usedBy":[]}""";
    private const string Procedure =
        """{"id":"one","description":"d","firstStep":"s","steps":{"s":{"listen":{}}},"revision":3,"notReady":[]}""";
    private const string Persona = """{"personality":"p","scope":null,"revision":3}""";
    private const string Renamed = """{"kind":"fact","oldId":"one","id":"two","revision":4,"referrers":[]}""";
    private const string Turned =
        """{"kind":"article","id":"one","off":true,"changed":true,"usedBy":[],"delivery":{"state":"Confirmed","number":1,"botNumber":1,"reason":null}}""";

    private readonly string folder = Directory.CreateTempSubdirectory("suwagcms-open-saved").FullName;

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private string File(string name, string text)
    {
        var path = Path.Combine(folder, name);
        System.IO.File.WriteAllText(path, text);
        return path;
    }

    private static StubServer Serving(string json) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

    private static async Task<CliResult> Run(string[] words, StubServer server, List<Uri> opened) =>
        await TestCli.Run([.. words, "--server", "http://cms.internal", "--bot", "bot"], server, openBrowser: opened.Add);

    private static async Task SavesThenOpens(string[] words, string json, string page)
    {
        var opened = new List<Uri>();
        var server = Serving(json);

        var result = await Run([.. words, "--open"], server, opened);

        Assert.True(result.ExitCode == 0, result.Error);
        Assert.NotEmpty(server.Requests);
        Assert.Equal($"http://cms.internal/bots/bot/{page}", Assert.Single(opened).AbsoluteUri);
    }

    [Theory]
    [Trait("criterion", "AC-cli-foundation-22")]
    [InlineData("facts add one --type text --description d --max-length 80", Fact, "facts?open=one")]
    [InlineData("facts edit one --description d", Fact, "facts?open=one")]
    [InlineData("kb add one --title T", Base, "knowledge-bases?open=one")]
    [InlineData("kb edit one --title T", Base, "knowledge-bases?open=one")]
    [InlineData("articles add one --kb offer --title T --content c --guide g --description d", Article, "articles?open=one")]
    [InlineData("articles edit one --title T", Article, "articles?open=one")]
    [InlineData("rename fact one two", Renamed, "facts?open=two")]
    [InlineData("availability off article one", Turned, "articles?open=one")]
    [InlineData("availability on article one", Turned, "articles?open=one")]
    public async Task A_command_that_changes_one_resource_saves_it_and_then_opens_it(string words, string json, string page)
    {
        ArgumentNullException.ThrowIfNull(words);

        await SavesThenOpens(words.Split(' '), json, page);
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-22")]
    public async Task An_action_a_procedure_and_the_persona_open_after_a_save_from_a_file()
    {
        var action = File("a.yaml", "id: one\ndescription: d\n");
        var procedure = File("p.yaml", "id: one\ndescription: d\n");
        var persona = File("s.yaml", "personality: p\n");

        await SavesThenOpens(["actions", "add", "--file", action], Action, "actions?open=one");
        await SavesThenOpens(["actions", "edit", "one", "--file", action], Action, "actions?open=one");
        await SavesThenOpens(["procedures", "add", "--file", procedure], Procedure, "procedures/one");
        await SavesThenOpens(["procedures", "edit", "one", "--file", procedure], Procedure, "procedures/one");
        await SavesThenOpens(["persona", "edit", "--file", persona], Persona, "persona");
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-22")]
    public async Task A_refused_change_opens_nothing()
    {
        var opened = new List<Uri>();
        var refused = StubServer.Answering(
            HttpStatusCode.UnprocessableEntity,
            """{"mistakes":[{"code":"x","path":"id","message":"No.","fix":"Fix."}]}"""
        );

        var result = await Run(["kb", "add", "one", "--title", "T", "--open"], refused, opened);

        Assert.Equal(1, result.ExitCode);
        Assert.Empty(opened);
    }

    [Theory]
    [Trait("criterion", "AC-cli-foundation-22")]
    [InlineData("facts edit one --description d --dry-run --open")]
    [InlineData("kb add one --title T --dry-run --open")]
    [InlineData("availability off article one --dry-run --open")]
    [InlineData("rename fact one two --dry-run --open")]
    [InlineData("facts edit one --description d --json --open")]
    [InlineData("availability on article one --json --open")]
    public async Task Open_with_a_dry_run_or_with_json_is_a_wrong_use_and_sends_nothing(string words)
    {
        ArgumentNullException.ThrowIfNull(words);
        var server = StubServer.Healthy();

        var result = await Run(words.Split(' '), server, []);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(server.Requests);
    }
}
