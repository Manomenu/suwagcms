using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Tests.Facts;

namespace Suwagcms.Server.Tests.Actions;

public sealed class ActionEndpointsTests(TestDatabase database, SharedApp shared) : IClassFixture<SharedApp>
{
    private TestApp App => shared.App;

    private HttpClient Client()
    {
        _ = database.Pool;
        return App.CreateClient();
    }

    private Task<ActionApi> Api() => ActionApi.NewBotAsync(Client());

    private static JsonNode Parsed(string json) => JsonNode.Parse(json) ?? throw new InvalidOperationException("no json");

    private static string Without(JsonElement shown)
    {
        var node = Parsed(shown.GetRawText()).AsObject();
        foreach (var name in new[] { "id", "revision", "usedBy", "alsoDeclared", "noLongerDeclared" })
        {
            node.Remove(name);
        }
        return Sorted(node);
    }

    // Why: the database keeps a document's keys in its own order, which means nothing, and null is a field left out.
    private static string Sorted(JsonNode? node) =>
        node switch
        {
            JsonObject map => "{"
                + string.Join(
                    ",",
                    map.Where(item => item.Value is not null)
                        .OrderBy(item => item.Key, StringComparer.Ordinal)
                        .Select(item => $"{item.Key}:{Sorted(item.Value)}")
                )
                + "}",
            JsonArray list => "[" + string.Join(",", list.Select(Sorted)) + "]",
            null => "null",
            _ => node.ToJsonString(),
        };

    [Fact]
    [Trait("criterion", "AC-http-actions-1")]
    public async Task An_action_with_parameters_results_headers_and_a_timeout_reads_back_as_written_alone_and_in_the_list()
    {
        var api = await Api();

        var saved = await api.Add("checkServiceability", ActionApi.Full);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var shown = await api.Show("checkServiceability");
        Assert.Equal(1, shown.GetProperty("revision").GetInt32());
        Assert.Equal(Sorted(Parsed(ActionApi.Full)), Without(shown));
        var listed = Assert.Single((await api.List()).EnumerateArray());
        Assert.Equal(Sorted(Parsed(ActionApi.Full)), Without(listed));
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-2")]
    public async Task An_action_with_only_an_id_a_description_and_an_implementation_is_saved()
    {
        var api = await Api();

        Assert.Equal(HttpStatusCode.OK, (await api.Add("send", ActionApi.Least)).StatusCode);

        Assert.Equal(Sorted(Parsed(ActionApi.Least)), Without(await api.Show("send")));
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-4")]
    public async Task A_bad_action_is_refused_with_every_mistake_and_nothing_is_saved()
    {
        var api = await Api();

        var refused = await api.Add("Bad-Id", """{"description":" ","parameters":{"a":{"type":"blob","description":" "}}}""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var codes = await FactApi.CodesOf(refused);
        foreach (
            var code in new[]
            {
                MistakeCodes.ActionIdInvalid,
                MistakeCodes.ActionDescriptionMissing,
                MistakeCodes.ActionImplementationMissing,
                MistakeCodes.FactTypeInvalid,
                MistakeCodes.FactDescriptionMissing,
            }
        )
        {
            Assert.Contains(code, codes);
        }
        Assert.Empty((await api.List()).EnumerateArray());
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-4")]
    public async Task Adding_an_id_the_bots_actions_already_have_is_refused()
    {
        var api = await Api();
        await api.Add("send", ActionApi.Least);

        var again = await api.Add("send", ActionApi.Least);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains(
            "already exists",
            (await FactApi.MistakesOf(again))[0].GetProperty("message").GetString(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-7")]
    public async Task A_saved_action_holds_its_secrets_only_as_names_in_every_answer_of_the_api()
    {
        var api = await Api();

        var saved = await api.Add("check", ActionApi.Full);

        foreach (
            var text in new[]
            {
                (await ActionApi.Json(saved)).GetRawText(),
                (await api.Show("check")).GetRawText(),
                (await api.List()).GetRawText(),
            }
        )
        {
            Assert.Contains("{secret:CRM_TOKEN}", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-7")]
    public async Task A_literal_secret_never_lands_in_a_refusal_or_in_the_store()
    {
        var api = await Api();

        var refused = await api.Add(
            "check",
            """{"description":"d","implementation":{"http":{"method":"POST","url":"https://x.internal","headers":{"Authorization":"Bearer leaked-value"}}}}"""
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.DoesNotContain(
            "leaked-value",
            await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            StringComparison.Ordinal
        );
        Assert.Equal(HttpStatusCode.NotFound, (await api.Get("check")).StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-8")]
    public async Task The_server_refuses_a_bad_action_with_the_codes_and_messages_of_the_validator_offline()
    {
        var api = await Api();
        const string Body =
            """{"description":"d","implementation":{"http":{"method":"GRAB","url":"https://x.internal/{none}","headers":{"Cookie":"a"}}}}""";
        var offline = DefinitionValidator.ValidateJson(
            """{"formatVersion":4,"actions":[{"id":"check","description":"d","implementation":{"http":{"method":"GRAB","url":"https://x.internal/{none}","headers":{"Cookie":"a"}}}}]}""",
            ValidationStage.Save
        );

        var refused = await FactApi.MistakesOf(await api.Add("check", Body));

        Assert.Equal(
            offline.Select(mistake => (mistake.Code, mistake.Path, mistake.Message)).Order(),
            refused
                .Select(mistake =>
                    (
                        mistake.GetProperty("code").GetString() ?? "",
                        mistake.GetProperty("path").GetString() ?? "",
                        mistake.GetProperty("message").GetString() ?? ""
                    )
                )
                .Order()
        );
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-9")]
    public async Task A_dry_run_gives_the_same_answer_and_changes_nothing()
    {
        var api = await Api();

        var dry = await api.Add("send", ActionApi.Least, "&dryRun=true");

        Assert.Equal(HttpStatusCode.OK, dry.StatusCode);
        Assert.Equal(1, (await ActionApi.Json(dry)).GetProperty("revision").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await api.Get("send")).StatusCode);

        await api.Add("send", ActionApi.Least);
        Assert.Equal(HttpStatusCode.NoContent, (await api.Delete("send", "?dryRun=true")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Get("send")).StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-9")]
    public async Task An_edit_or_delete_of_an_old_revision_is_refused_with_the_current_one_and_an_unknown_field_is_named()
    {
        var api = await Api();
        await api.Add("send", ActionApi.Least);
        await api.Put("send", ActionApi.Least, "?ifRevision=1");

        var edit = await api.Put("send", ActionApi.Least, "?ifRevision=1");
        var delete = await api.Delete("send", "?ifRevision=1");
        var unknown = await api.Add("other", """{"description":"d","secretValue":"x"}""");

        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Contains("revision 2", (await FactApi.MistakesOf(edit))[0].GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        Assert.Contains("revision 2", (await FactApi.MistakesOf(delete))[0].GetProperty("message").GetString(), StringComparison.Ordinal);
        var named = await FactApi.MistakesOf(unknown);
        Assert.Equal("secretValue", named[0].GetProperty("path").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-10")]
    public async Task An_action_of_one_bot_is_not_listed_for_another_and_two_bots_can_have_one_id()
    {
        var one = await Api();
        var other = await Api();

        await one.Add("send", ActionApi.Least);

        Assert.Empty((await other.List()).EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await other.Add("send", ActionApi.Full)).StatusCode);
        Assert.Equal("Sends.", (await one.Show("send")).GetProperty("description").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-11")]
    public async Task An_unused_action_can_be_deleted_and_is_gone()
    {
        var api = await Api();
        await api.Add("send", ActionApi.Least);

        Assert.Equal(HttpStatusCode.NoContent, (await api.Delete("send")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await api.Get("send")).StatusCode);
        Assert.Empty((await api.List()).EnumerateArray());
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-12")]
    public async Task While_an_action_is_added_edited_shown_or_deleted_the_cms_sends_nothing_to_its_url()
    {
        var calls = 0;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        await using var service = builder.Build();
        service.Run(context =>
        {
            Interlocked.Increment(ref calls);
            return context.Response.WriteAsync("{}");
        });
        await service.StartAsync(TestContext.Current.CancellationToken);
        var api = await Api();
        var body = """{"description":"d","implementation":{"http":{"method":"GET","url":"URL/check"}}}""".Replace(
            "URL",
            service.Urls.Single(),
            StringComparison.Ordinal
        );

        await api.Add("check", body);
        await api.Put("check", body, "?ifRevision=1");
        await api.Show("check");
        await api.List();
        await api.Delete("check");

        Assert.Equal(0, Volatile.Read(ref calls));
        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> Conversation(HttpClient client, string bot) =>
        await client.PutAsJsonAsync(
            $"/api/bots/{bot}/procedures/conversation",
            new
            {
                description = "The base.",
                firstStep = "wait",
                steps = new { wait = new { listen = new { }, next = "answer" }, answer = new { say = "Hi", next = "wait" } },
            },
            TestContext.Current.CancellationToken
        );

    private static async Task<int> NewVersion(HttpClient client, string bot)
    {
        var created = await client.PostAsync($"/api/bots/{bot}/versions", null, TestContext.Current.CancellationToken);
        return (await ActionApi.Json(created)).GetProperty("version").GetProperty("number").GetInt32();
    }

    [Fact]
    [Trait("criterion", "AC-version-create-3")]
    public async Task A_version_holds_the_bots_actions()
    {
        var client = Client();
        var api = await ActionApi.NewBotAsync(client);
        (await Conversation(client, api.Bot)).EnsureSuccessStatusCode();
        await api.Add("check", ActionApi.Full);

        var number = await NewVersion(client, api.Bot);

        var shown = await ActionApi.Json(
            await client.GetAsync($"/api/bots/{api.Bot}/versions/{number}", TestContext.Current.CancellationToken)
        );
        Assert.Equal(1, shown.GetProperty("version").GetProperty("actions").GetInt32());
        var document = JsonDocument.Parse(shown.GetProperty("document").GetString() ?? "");
        var action = Assert.Single(document.RootElement.GetProperty("actions").EnumerateArray());
        Assert.Equal("check", action.GetProperty("id").GetString());
        Assert.Empty(DefinitionValidator.ValidateJson(document.RootElement.GetRawText(), ValidationStage.Version));
    }

    [Fact]
    [Trait("criterion", "AC-version-diff-14")]
    public async Task A_comparison_covers_actions_with_their_contract_and_implementation_and_secrets_only_as_references()
    {
        var client = Client();
        var api = await ActionApi.NewBotAsync(client);
        (await Conversation(client, api.Bot)).EnsureSuccessStatusCode();
        await api.Add("check", ActionApi.Least);
        var from = await NewVersion(client, api.Bot);
        await api.Put("check", ActionApi.Full);
        var to = await NewVersion(client, api.Bot);

        var compared = await ActionApi.Json(
            await client.GetAsync($"/api/bots/{api.Bot}/versions/{from}/diff/{to}", TestContext.Current.CancellationToken)
        );

        var resource = Assert.Single(compared.GetProperty("resources").EnumerateArray());
        Assert.Equal("Action", resource.GetProperty("kind").GetString());
        var fields = resource.GetProperty("fields").EnumerateArray().Select(field => field.GetProperty("field").GetString()).ToList();
        Assert.Contains("parameters", fields);
        Assert.Contains("results", fields);
        Assert.Contains("implementation.http.url", fields);
        Assert.Contains("{secret:CRM_TOKEN}", resource.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-export-1")]
    public async Task An_export_covers_actions_with_secrets_only_as_references()
    {
        var client = Client();
        var api = await ActionApi.NewBotAsync(client);
        (await Conversation(client, api.Bot)).EnsureSuccessStatusCode();
        await api.Add("check", ActionApi.Full);
        var number = await NewVersion(client, api.Bot);

        var export = await ActionApi.Json(
            await client.GetAsync($"/api/bots/{api.Bot}/versions/{number}/export", TestContext.Current.CancellationToken)
        );

        var file = Assert.Single(
            export.GetProperty("files").EnumerateArray(),
            file => file.GetProperty("path").GetString() == "actions/check.yaml"
        );
        Assert.Contains("{secret:CRM_TOKEN}", file.GetProperty("data").GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-change-history-1")]
    public async Task History_covers_actions_with_secrets_only_as_the_references_they_hold()
    {
        var client = Client();
        var api = await ActionApi.NewBotAsync(client);
        await api.Add("check", ActionApi.Full);
        await api.Delete("check");

        var page = await ActionApi.Json(
            await client.GetAsync($"/api/bots/{api.Bot}/actions/check/history", TestContext.Current.CancellationToken)
        );

        var entries = page.GetProperty("entries").EnumerateArray().ToList();
        Assert.Equal(["deleted", "added"], entries.Select(entry => entry.GetProperty("action").GetString()));
        Assert.All(entries, entry => Assert.Equal("action", entry.GetProperty("kind").GetString()));
        Assert.Contains("{secret:CRM_TOKEN}", entries[1].GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-1")]
    public async Task A_file_of_changes_covers_actions_with_the_same_refusals()
    {
        var client = Client();
        var api = await ActionApi.NewBotAsync(client);
        var implementation = new { http = new { method = "POST", url = "https://x.internal/a" } };

        var added = await client.PostAsJsonAsync(
            $"/api/bots/{api.Bot}/changes",
            new
            {
                changes = new object[]
                {
                    new
                    {
                        add = "action",
                        id = "send",
                        description = "Sends.",
                        implementation,
                    },
                },
            },
            TestContext.Current.CancellationToken
        );
        var refused = await client.PostAsJsonAsync(
            $"/api/bots/{api.Bot}/changes",
            new
            {
                changes = new object[]
                {
                    new
                    {
                        edit = "action",
                        id = "send",
                        implementation = new { http = new { method = "POST", url = "https://x.internal/{secret:A}" } },
                    },
                },
            },
            TestContext.Current.CancellationToken
        );
        var deleted = await client.PostAsJsonAsync(
            $"/api/bots/{api.Bot}/changes",
            new { changes = new object[] { new { delete = "action", id = "send" } } },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(1, (await ActionApi.Json(added)).GetProperty("added").GetInt32());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Contains(MistakeCodes.ActionSecretInUrl, await FactApi.CodesOf(refused));
        Assert.Equal(1, (await ActionApi.Json(deleted)).GetProperty("deleted").GetInt32());
    }
}
