using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Suwagcms.Cli.Tests.Plugin;

namespace Suwagcms.Cli.Tests.Actions;

public sealed class ActionsTests : IDisposable
{
    private static readonly string[] OnBot = ["--server", "http://cms.internal", "--bot", "rzeczoznawca"];

    private const string Yaml = """
        id: checkServiceability
        description: Checks whether we serve an address.
        parameters:
          address: { type: text, maxLength: 200, description: "Street, number and city." }
          floors: { type: int, min: 1, max: 40, description: Floors. }
        results:
          serviceable: { type: enum, values: [yes, no], description: Whether we serve it. }
        implementation:
          http:
            method: POST
            url: https://crm.internal/serviceability
            timeoutSeconds: 20
            headers:
              Authorization: Bearer {secret:CRM_TOKEN}
        """;

    private const string Shown = """
        {"id":"checkServiceability","description":"Checks whether we serve an address.",
         "parameters":{"address":{"type":"text","description":"Street, number and city.","values":null,"min":null,"max":null,"maxLength":200},"floors":{"type":"int","description":"Floors.","values":null,"min":1,"max":40,"maxLength":null}},
         "results":{"serviceable":{"type":"enum","description":"Whether we serve it.","values":["yes","no"],"min":null,"max":null,"maxLength":null}},
         "implementation":{"http":{"method":"POST","url":"https://crm.internal/serviceability","headers":{"Authorization":"Bearer {secret:CRM_TOKEN}"},"timeoutSeconds":20}},
         "revision":3,"usedBy":["orderService"]}
        """;

    private readonly string folder = Path.Combine(Path.GetTempPath(), "suwagcms-cli-tests", Guid.NewGuid().ToString("N"));

    public ActionsTests()
    {
        Directory.CreateDirectory(folder);
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private string Saved(string name, string text)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, text);
        return path;
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    // Why: what the server would read: the sent bodies, keyed by the address and method.
    private static StubServer Recording(List<(string Method, string Path, string Body)> sent, string answer = Shown) =>
        new(request =>
        {
            using var reader = new StreamReader(request.Content?.ReadAsStream() ?? Stream.Null);
            var body = reader.ReadToEnd();
            sent.Add((request.Method.Method, request.RequestUri?.PathAndQuery ?? "", body));
            return request.Method == HttpMethod.Delete ? new HttpResponseMessage(HttpStatusCode.NoContent) : Json(answer);
        });

    private static readonly string[] AsJson = ["--json"];

    private static string Text(JsonNode? node) =>
        node switch
        {
            JsonObject map => "{"
                + string.Join(
                    ",",
                    map.Where(item => item.Value is not null)
                        .OrderBy(item => item.Key, StringComparer.Ordinal)
                        .Select(item => $"{item.Key}:{Text(item.Value)}")
                )
                + "}",
            JsonArray list => "[" + string.Join(",", list.Select(Text)) + "]",
            null => "null",
            _ => node.ToJsonString(),
        };

    [Fact]
    [Trait("criterion", "AC-http-actions-1")]
    public async Task An_action_added_from_yaml_is_sent_as_written_with_numbers_as_numbers()
    {
        var sent = new List<(string Method, string Path, string Body)>();

        var result = await TestCli.Run(["actions", "add", "--file", Saved("a.yaml", Yaml), .. OnBot], Recording(sent));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Added checkServiceability, revision 3.", result.Output, StringComparison.Ordinal);
        var (method, path, body) = Assert.Single(sent);
        Assert.Equal("PUT", method);
        Assert.Equal("/api/bots/rzeczoznawca/actions/checkServiceability?ifRevision=0", path);
        var node = JsonNode.Parse(body);
        Assert.Null(node?["id"]);
        Assert.Equal(200, node?["parameters"]?["address"]?["maxLength"]?.GetValue<int>());
        Assert.Equal(40, node?["parameters"]?["floors"]?["max"]?.GetValue<int>());
        Assert.Equal("yes", node?["results"]?["serviceable"]?["values"]?[0]?.GetValue<string>());
        Assert.Equal(20, node?["implementation"]?["http"]?["timeoutSeconds"]?.GetValue<int>());
        Assert.Equal("Bearer {secret:CRM_TOKEN}", node?["implementation"]?["http"]?["headers"]?["Authorization"]?.GetValue<string>());
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-1")]
    public async Task List_and_show_read_back_in_yaml_and_json()
    {
        var server = new StubServer(request =>
            Json(request.RequestUri?.AbsolutePath.EndsWith("/actions", StringComparison.Ordinal) == true ? $"[{Shown}]" : Shown)
        );

        var list = await TestCli.Run(["actions", "list", .. OnBot], server);
        var listJson = await TestCli.Run(["actions", "list", "--json", .. OnBot], server);
        var yaml = await TestCli.Run(["actions", "show", "checkServiceability", .. OnBot], server);
        var json = await TestCli.Run(["actions", "show", "checkServiceability", "--json", .. OnBot], server);

        Assert.Contains("checkServiceability  Checks whether we serve an address.  http", list.Output, StringComparison.Ordinal);
        Assert.Equal("checkServiceability", JsonNode.Parse(listJson.Output)?["actions"]?[0]?["id"]?.GetValue<string>());
        Assert.Contains("url: https://crm.internal/serviceability", yaml.Output, StringComparison.Ordinal);
        Assert.Contains("Authorization: Bearer {secret:CRM_TOKEN}", yaml.Output, StringComparison.Ordinal);
        Assert.Equal(200, JsonNode.Parse(json.Output)?["parameters"]?["address"]?["maxLength"]?.GetValue<int>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("criterion", "AC-http-actions-3")]
    public async Task The_output_of_show_is_accepted_back_by_edit_without_a_change_of_meaning(bool asJson)
    {
        var sent = new List<(string Method, string Path, string Body)>();
        var server = Recording(sent);
        var shown = await TestCli.Run(["actions", "show", "checkServiceability", .. asJson ? AsJson : [], .. OnBot], server);
        sent.Clear();

        var result = await TestCli.Run(
            ["actions", "edit", "checkServiceability", "--file", Saved("shown.txt", shown.Output), .. OnBot],
            server
        );

        Assert.Equal(0, result.ExitCode);
        var put = Assert.Single(sent, call => call.Method == "PUT");
        Assert.EndsWith("?ifRevision=3", put.Path, StringComparison.Ordinal);
        var expected = (JsonObject?)JsonNode.Parse(Shown);
        expected?.Remove("id");
        expected?.Remove("revision");
        expected?.Remove("usedBy");
        Assert.Equal(Text(expected), Text(JsonNode.Parse(put.Body)));
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-6")]
    public async Task A_refusal_prints_each_mistake_with_its_fix_and_exits_1()
    {
        const string Refusal =
            """{"mistakes":[{"code":"action.secret.literal","stage":"Save","resource":{"kind":"Action","id":"check"},"path":"implementation.http.headers.Authorization","message":"Header 'Authorization' holds a literal value, and the CMS never stores secrets.","fix":"Write 'Bearer {secret:CRM_TOKEN}' and set CRM_TOKEN in the bot's environment.","related":[]}]}""";
        var server = new StubServer(_ => Json(Refusal, HttpStatusCode.UnprocessableEntity));

        var result = await TestCli.Run(["actions", "add", "--file", Saved("a.yaml", Yaml), .. OnBot], server);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("action.secret.literal", result.Error, StringComparison.Ordinal);
        Assert.Contains("Bearer {secret:CRM_TOKEN}", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-9")]
    public async Task Dry_run_on_add_and_delete_is_sent_as_one_and_says_nothing_changed()
    {
        var sent = new List<(string Method, string Path, string Body)>();

        var add = await TestCli.Run(["actions", "add", "--file", Saved("a.yaml", Yaml), "--dry-run", .. OnBot], Recording(sent));
        var delete = await TestCli.Run(["actions", "delete", "checkServiceability", "--dry-run", .. OnBot], Recording(sent));

        Assert.Contains("Nothing was changed", add.Output, StringComparison.Ordinal);
        Assert.Contains("Nothing was changed", delete.Output, StringComparison.Ordinal);
        Assert.All(sent, call => Assert.Contains("dryRun=true", call.Path, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-11")]
    public async Task Delete_says_the_action_is_gone()
    {
        var sent = new List<(string Method, string Path, string Body)>();

        var result = await TestCli.Run(["actions", "delete", "checkServiceability", .. OnBot], Recording(sent));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Deleted 'checkServiceability'.", result.Output, StringComparison.Ordinal);
        Assert.Equal("DELETE", Assert.Single(sent).Method);
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-19")]
    public void The_main_skill_has_a_line_for_each_command_and_says_how_a_secret_is_written()
    {
        var skill = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "claude-plugin", "skills", "suwagcms", "SKILL.md"));
        var listed = CommandList.Listed(skill);

        foreach (var command in new[] { "actions list", "actions show", "actions add", "actions edit", "actions delete" })
        {
            Assert.Contains(command, listed);
        }
        Assert.Contains("{secret:", skill, StringComparison.Ordinal);
        Assert.Contains("## Action file", skill, StringComparison.Ordinal);
    }

    private const string Code =
        """{"id":"lookupPolicy","description":"Finds a policy.","parameters":{"pesel":{"type":"text","description":"PESEL.","maxLength":11}},"results":null,"implementation":"code","revision":1,"usedBy":[],"alsoDeclared":false,"noLongerDeclared":false}""";

    [Fact]
    [Trait("criterion", "AC-code-actions-1")]
    public async Task A_code_action_is_listed_and_shown_marked_code_with_no_call()
    {
        var server = new StubServer(request =>
            Json(request.RequestUri?.AbsolutePath.EndsWith("/actions", StringComparison.Ordinal) == true ? $"[{Code}]" : Code)
        );

        var list = await TestCli.Run(["actions", "list", .. OnBot], server);
        var show = await TestCli.Run(["actions", "show", "lookupPolicy", .. OnBot], server);

        Assert.Contains("lookupPolicy  Finds a policy.  code", list.Output, StringComparison.Ordinal);
        Assert.Contains("implementation: code", show.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("url:", show.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-2")]
    public async Task The_list_and_its_json_give_both_kinds_each_with_its_implementation()
    {
        var server = new StubServer(_ => Json($"[{Code},{Shown}]"));

        var text = await TestCli.Run(["actions", "list", .. OnBot], server);
        var json = await TestCli.Run(["actions", "list", "--json", .. OnBot], server);

        Assert.Contains("code", text.Output, StringComparison.Ordinal);
        Assert.Contains("http", text.Output, StringComparison.Ordinal);
        var actions = JsonNode.Parse(json.Output)?["actions"];
        Assert.Equal("code", actions?[0]?["implementation"]?.GetValue<string>());
        Assert.Equal("POST", actions?[1]?["implementation"]?["http"]?["method"]?.GetValue<string>());
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-7")]
    public async Task Editing_a_code_action_prints_the_refusal_with_the_fix_and_exits_1()
    {
        const string Refusal =
            """{"mistakes":[{"code":"action.code.readOnly","stage":"Save","resource":{"kind":"Action","id":"lookupPolicy"},"path":"implementation","message":"'lookupPolicy' is written in the bot's code, so the CMS only shows it.","fix":"Change it in the bot and deploy; the CMS sees the change the next time it asks.","related":[]}]}""";
        var server = new StubServer(request =>
            request.Method == HttpMethod.Put ? Json(Refusal, HttpStatusCode.UnprocessableEntity) : Json(Code)
        );

        var result = await TestCli.Run(["actions", "edit", "lookupPolicy", "--file", Saved("c.json", Code), .. OnBot], server);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("action.code.readOnly", result.Error, StringComparison.Ordinal);
        Assert.Contains("Change it in the bot and deploy", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-9")]
    public async Task The_list_shows_an_http_action_whose_id_the_bot_also_declares()
    {
        var conflict = Shown.Replace("\"usedBy\":[\"orderService\"]", "\"usedBy\":[],\"alsoDeclared\":true", StringComparison.Ordinal);
        var server = new StubServer(_ => Json($"[{conflict}]"));

        var result = await TestCli.Run(["actions", "list", .. OnBot], server);

        Assert.Contains("http  the bot also declares this id", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-14")]
    public void The_main_skill_says_in_the_lines_of_actions_that_a_code_action_is_read_only_and_where_to_change_it()
    {
        var skill = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "claude-plugin", "skills", "suwagcms", "SKILL.md"));
        var lines = skill
            .ReplaceLineEndings("\n")
            .Split('\n')
            .Where(line => line.StartsWith("- `suwagcms actions ", StringComparison.Ordinal));

        Assert.Contains(
            lines,
            line => line.Contains("A code action is read-only: change it in the bot's code and deploy", StringComparison.Ordinal)
        );
    }
}
