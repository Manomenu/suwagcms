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

    private const string Step = """
        id: orderService
        description: Orders a service.
        firstStep: checkAddress
        steps:
          checkAddress:
            action: checkServiceability
            with: { address: customer.address }
            results: { serviceable: isServiceable }
            onFailure: tellItFailed
            next:
              - when: { isServiceable: { oneOf: [yes] } }
                to: offerPlan
              - to: tellItFailed
          offerPlan:
            say: We serve you.
          tellItFailed:
            say: Sorry.
        """;

    [Fact]
    [Trait("criterion", "AC-action-block-1")]
    public async Task A_procedure_with_an_action_step_is_sent_as_written_and_its_show_output_is_accepted_back()
    {
        var sent = new List<(string Method, string Path, string Body)>();
        var added =
            """{"id":"orderService","description":"Orders a service.","firstStep":"checkAddress","steps":{"checkAddress":{"action":"checkServiceability","with":{"address":"customer.address"},"results":{"serviceable":"isServiceable"},"onFailure":"tellItFailed","next":[{"when":{"isServiceable":{"oneOf":["yes"]}},"to":"offerPlan"},{"to":"tellItFailed"}]},"offerPlan":{"say":"We serve you."},"tellItFailed":{"say":"Sorry."}},"whenStalled":null,"revision":1,"notReady":[],"localFacts":null}""";

        var add = await TestCli.Run(["procedures", "add", "--file", Saved("p.yaml", Step), .. OnBot], Recording(sent, added));
        var yaml = await TestCli.Run(["procedures", "show", "orderService", .. OnBot], Recording(sent, added));
        var json = await TestCli.Run(["procedures", "show", "orderService", "--json", .. OnBot], Recording(sent, added));
        var bodies = new List<string> { Assert.Single(sent, call => call.Method == "PUT").Body };
        foreach (var (name, shown) in new[] { ("y.yaml", yaml.Output), ("j.json", json.Output) })
        {
            sent.Clear();
            var edit = await TestCli.Run(
                ["procedures", "edit", "orderService", "--file", Saved(name, shown), .. OnBot],
                Recording(sent, added)
            );
            Assert.Equal(0, edit.ExitCode);
            bodies.Add(Assert.Single(sent, call => call.Method == "PUT").Body);
        }

        Assert.Equal(0, add.ExitCode);
        Assert.Equal("isServiceable", JsonNode.Parse(bodies[0])?["steps"]?["checkAddress"]?["results"]?["serviceable"]?.GetValue<string>());
        var first = Text(JsonNode.Parse(bodies[0]));
        Assert.All(bodies, body => Assert.Equal(first, Text(JsonNode.Parse(body))));
    }

    [Fact]
    [Trait("criterion", "AC-action-block-11")]
    public async Task An_unfinished_action_step_is_saved_and_the_cli_lists_it_and_exits_0()
    {
        const string Answer =
            """{"id":"p","description":"d","firstStep":"run","steps":{},"whenStalled":null,"revision":1,"notReady":[{"code":"step.action.noOnFailure","stage":"Version","resource":{"kind":"Procedure","id":"p"},"path":"steps.run.onFailure","message":"Step 'run' has no onFailure.","fix":"Add the step to go to when the action fails.","related":[]}],"localFacts":null}""";

        var result = await TestCli.Run(["procedures", "add", "--file", Saved("p.yaml", Step), .. OnBot], new StubServer(_ => Json(Answer)));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("step.action.noOnFailure", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-action-block-15")]
    public void The_lines_of_procedures_in_the_main_skill_give_the_format_of_the_action_block()
    {
        var skill = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "claude-plugin", "skills", "suwagcms", "SKILL.md"));
        var line = skill
            .ReplaceLineEndings("\n")
            .Split('\n')
            .Single(text => text.StartsWith("- `suwagcms procedures add", StringComparison.Ordinal));

        foreach (var field in new[] { "`action`", "`with`", "`results`", "`onFailure`" })
        {
            Assert.Contains(field, line, StringComparison.Ordinal);
        }
        Assert.Contains("action: checkServiceability", skill, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-action-block-17")]
    public async Task Usage_of_a_procedure_prints_the_action_and_each_mapped_fact_with_the_field()
    {
        const string Usage =
            """{"usedBy":[],"uses":[{"kind":"action","id":"checkServiceability","field":"steps.checkAddress.action"},{"kind":"fact","id":"customer.address","field":"steps.checkAddress.with.address"}]}""";

        var result = await TestCli.Run(["usage", "procedure", "orderService", .. OnBot], new StubServer(_ => Json(Usage)));

        Assert.Contains("action checkServiceability (steps.checkAddress.action)", result.Output, StringComparison.Ordinal);
        Assert.Contains("fact customer.address (steps.checkAddress.with.address)", result.Output, StringComparison.Ordinal);
    }

    private const string Get = """
        {"id":"lookup","description":"Looks.","parameters":{"address":{"type":"text","description":"Where.","maxLength":20}},"results":null,"implementation":{"http":{"method":"GET","url":"https://crm.internal/lookup"}},"revision":1,"usedBy":[],"alsoDeclared":false,"noLongerDeclared":false}
        """;

    private const string Won =
        """{"outcome":"success","results":{"serviceable":"yes"},"reason":null,"status":200,"elapsedMs":320,"answerStart":"{}","answerCut":false}""";

    private static StubServer Testing(string action, string answer) =>
        new(request => request.Method == HttpMethod.Post ? Json(answer) : Json(action));

    [Fact]
    [Trait("criterion", "AC-action-test-run-1")]
    public async Task A_run_prints_success_with_the_results_the_status_and_the_time_and_exits_0()
    {
        var server = Testing(Get, Won);

        var result = await TestCli.Run(["actions", "test", "lookup", "--param", "address=Polna 3, Suwałki", .. OnBot], server);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Success. Answer 200. 320 ms.", result.Output, StringComparison.Ordinal);
        Assert.Contains("serviceable = yes", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-4")]
    public async Task A_failure_prints_the_reason_the_status_and_the_start_of_the_answer_and_exits_1()
    {
        const string Lost =
            """{"outcome":"failure","results":{},"reason":"Answer 200: result 'serviceable' is missing from the answer.","status":200,"elapsedMs":320,"answerStart":"{\"status\":\"ok\"}","answerCut":false}""";

        var result = await TestCli.Run(["actions", "test", "lookup", .. OnBot], Testing(Get, Lost));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Failed: Answer 200: result 'serviceable' is missing", result.Error, StringComparison.Ordinal);
        Assert.Contains("{\"status\":\"ok\"}", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-5")]
    public async Task Values_the_server_refuses_are_printed_with_their_fixes_and_exit_1()
    {
        const string Refusal =
            """{"mistakes":[{"code":"action.test.value","stage":"Save","resource":{"kind":"Action","id":"lookup"},"path":"values.address","message":"Parameter 'address' has no value.","fix":"Give it a value.","related":[]}]}""";
        var server = new StubServer(request =>
            request.Method == HttpMethod.Post ? Json(Refusal, HttpStatusCode.UnprocessableEntity) : Json(Get)
        );

        var result = await TestCli.Run(["actions", "test", "lookup", .. OnBot], server);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Parameter 'address' has no value.", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("unreachable", "The bot could not be asked: it is down. The action has not been judged.")]
    [InlineData(
        "notOffered",
        "Bot rzeczoznawca does not offer test runs. Update its Suwagcms.Sdk and deploy; the action has not been judged."
    )]
    [Trait("criterion", "AC-action-test-run-6")]
    [Trait("criterion", "AC-action-test-run-7")]
    public async Task A_bot_not_asked_is_told_apart_from_a_failed_action_in_the_words_and_in_json_and_exits_1(string outcome, string reason)
    {
        var answer =
            $$"""{"outcome":"{{outcome}}","results":{},"reason":"{{reason}}","status":null,"elapsedMs":0,"answerStart":null,"answerCut":false}""";

        var text = await TestCli.Run(["actions", "test", "lookup", .. OnBot], Testing(Get, answer));
        var json = await TestCli.Run(["actions", "test", "lookup", "--json", .. OnBot], Testing(Get, answer));

        Assert.Equal(1, text.ExitCode);
        Assert.Contains($"Not judged: {reason}", text.Error, StringComparison.Ordinal);
        Assert.Equal(1, json.ExitCode);
        Assert.Equal(outcome, JsonNode.Parse(json.Output)?["outcome"]?.GetValue<string>());
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-9")]
    public async Task A_cut_answer_says_so()
    {
        var long1 = new string('x', 2000);
        var answer =
            $$"""{"outcome":"failure","results":{},"reason":"The service answered 500, outside 2xx.","status":500,"elapsedMs":1,"answerStart":"{{long1}}","answerCut":true}""";

        var result = await TestCli.Run(["actions", "test", "lookup", .. OnBot], Testing(Get, answer));

        Assert.Contains("cut at its first 2000 characters", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-12")]
    public async Task A_get_runs_at_once_and_another_method_without_yes_prints_the_call_sends_nothing_and_exits_1()
    {
        var posts = new List<string>();
        StubServer Counting(string action)
        {
            return new(request =>
            {
                if (request.Method == HttpMethod.Post)
                {
                    posts.Add(request.RequestUri?.AbsolutePath ?? "");
                    return Json(Won);
                }
                return Json(action);
            });
        }

        var get = await TestCli.Run(["actions", "test", "lookup", .. OnBot], Counting(Get));
        Assert.Equal(0, get.ExitCode);
        Assert.Single(posts);
        posts.Clear();

        var refused = await TestCli.Run(["actions", "test", "checkServiceability", .. OnBot], Counting(Shown));
        Assert.Equal(1, refused.ExitCode);
        Assert.Contains("POST https://crm.internal/serviceability", refused.Error, StringComparison.Ordinal);
        Assert.Contains("--yes", refused.Error, StringComparison.Ordinal);
        Assert.Empty(posts);

        var yes = await TestCli.Run(["actions", "test", "checkServiceability", "--yes", .. OnBot], Counting(Shown));
        Assert.Equal(0, yes.ExitCode);
        Assert.Single(posts);
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-16")]
    public async Task A_code_action_asks_first_as_a_call_that_may_do_work()
    {
        var result = await TestCli.Run(["actions", "test", "lookupPolicy", .. OnBot], Testing(Code, Won));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("really runs the bot's code", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-14")]
    public async Task Test_with_json_returns_the_outcome_and_the_main_skill_says_a_run_really_calls_the_service()
    {
        var json = await TestCli.Run(["actions", "test", "lookup", "--json", .. OnBot], Testing(Get, Won));

        var answer = JsonNode.Parse(json.Output);
        Assert.Equal("success", answer?["outcome"]?.GetValue<string>());
        Assert.Equal("yes", answer?["results"]?["serviceable"]?.GetValue<string>());
        Assert.Equal(200, answer?["status"]?.GetValue<int>());
        Assert.Equal(320, answer?["elapsedMs"]?.GetValue<int>());
        var skill = await File.ReadAllTextAsync(
            Path.Combine(RepositoryRoot.Path, "claude-plugin", "skills", "suwagcms", "SKILL.md"),
            TestContext.Current.CancellationToken
        );
        Assert.Contains("actions test", CommandList.Listed(skill));
        Assert.Contains("A test run really calls the service", skill, StringComparison.Ordinal);
    }
}
