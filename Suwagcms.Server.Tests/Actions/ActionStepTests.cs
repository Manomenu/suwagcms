using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.Procedures.Blocks;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.Versions;

namespace Suwagcms.Server.Tests.Actions;

public sealed class ActionStepTests(TestDatabase database) : IDisposable
{
    private const string Check = """
        {"description":"Checks.",
         "parameters":{"address":{"type":"text","maxLength":200,"description":"Where."}},
         "results":{"serviceable":{"type":"enum","values":["yes","no"],"description":"Served."}},
         "implementation":{"http":{"method":"POST","url":"https://crm.internal/check"}}}
        """;

    private const string Order = """
        {"description":"Orders.","firstStep":"checkAddress","steps":{
          "checkAddress":{"action":"checkServiceability","with":{"address":"customer.address"},"results":{"serviceable":"isServiceable"},"onFailure":"failed",
            "next":[{"when":{"isServiceable":{"oneOf":["yes"]}},"to":"offer"},{"to":"failed"}]},
          "offer":{"say":"We serve you."},
          "failed":{"say":"Sorry."}}}
        """;

    private readonly TestApp app = new();
    private readonly List<IDisposable> disposables = [];

    public void Dispose()
    {
        foreach (var item in disposables)
        {
            item.Dispose();
        }
        app.Dispose();
    }

    private sealed record Scene(HttpClient Client, ActionApi Actions, FactApi Facts)
    {
        public string Bot => Actions.Bot;

        public async Task<HttpResponseMessage> PutProcedure(string id, string json, string query = "")
        {
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            return await Client.PutAsync($"/api/bots/{Bot}/procedures/{id}{query}", content, TestContext.Current.CancellationToken);
        }

        public async Task<JsonElement> Procedure(string id) =>
            await ActionApi.Json(await Client.GetAsync($"/api/bots/{Bot}/procedures/{id}", TestContext.Current.CancellationToken));

        public Task<HttpResponseMessage> Rename(string collection, string id, string newId, string query = "") =>
            Client.PostAsJsonAsync(
                $"/api/bots/{Bot}/{collection}/{id}/rename{query}",
                new { newId },
                TestContext.Current.CancellationToken
            );

        public async Task AddFactsAsync()
        {
            (await Facts.Add("customer.address", FactApi.Text("The address", 200))).EnsureSuccessStatusCode();
            (
                await Facts.Add("isServiceable", """{"type":"enum","description":"Served","values":["yes","no"],"writableBy":"assign"}""")
            ).EnsureSuccessStatusCode();
        }
    }

    private async Task<Scene> SceneAsync(bool withAction = true)
    {
        _ = database.Pool;
        var client = app.CreateClient();
        disposables.Add(client);
        var facts = await FactApi.NewBotAsync(client);
        var scene = new Scene(client, new ActionApi(client, facts.Bot), facts);
        await scene.AddFactsAsync();
        if (withAction)
        {
            (await scene.Actions.Add("checkServiceability", Check)).EnsureSuccessStatusCode();
        }
        return scene;
    }

    [Fact]
    [Trait("criterion", "AC-action-block-1")]
    public async Task A_procedure_with_an_action_step_reads_back_as_written()
    {
        var scene = await SceneAsync();

        var saved = await scene.PutProcedure("orderService", Order, "?ifRevision=0");

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var step = (await scene.Procedure("orderService")).GetProperty("steps").GetProperty("checkAddress");
        Assert.Equal("checkServiceability", step.GetProperty("action").GetString());
        Assert.Equal("customer.address", step.GetProperty("with").GetProperty("address").GetString());
        Assert.Equal("isServiceable", step.GetProperty("results").GetProperty("serviceable").GetString());
        Assert.Equal("failed", step.GetProperty("onFailure").GetString());
        Assert.Equal("offer", step.GetProperty("next")[0].GetProperty("to").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-action-block-2")]
    public async Task One_action_serves_two_procedures_mapping_to_different_facts_and_neither_save_changes_it()
    {
        var scene = await SceneAsync();
        (await scene.Facts.Add("other.address", FactApi.Text("Another", 100))).EnsureSuccessStatusCode();

        (await scene.PutProcedure("orderService", Order)).EnsureSuccessStatusCode();
        (
            await scene.PutProcedure("second", Order.Replace("customer.address", "other.address", StringComparison.Ordinal))
        ).EnsureSuccessStatusCode();

        var action = await scene.Actions.Show("checkServiceability");
        Assert.Equal(1, action.GetProperty("revision").GetInt32());
        Assert.DoesNotContain("address\":\"customer", action.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(["orderService", "second"], action.GetProperty("usedBy").EnumerateArray().Select(name => name.GetString()));
    }

    [Fact]
    [Trait("criterion", "AC-action-block-11")]
    public async Task A_procedure_with_only_unfinished_action_steps_is_saved_and_the_answer_lists_them()
    {
        var scene = await SceneAsync();
        const string Unfinished = """{"description":"Orders.","firstStep":"run","steps":{"run":{"action":"checkServiceability"}}}""";

        var saved = await ActionApi.Json(await scene.PutProcedure("orderService", Unfinished));

        var codes = saved.GetProperty("notReady").EnumerateArray().Select(mistake => mistake.GetProperty("code").GetString()).ToList();
        Assert.Contains(MistakeCodes.StepActionNoOnFailure, codes);
        Assert.Contains(MistakeCodes.StepActionUnmappedParameter, codes);
    }

    [Fact]
    [Trait("criterion", "AC-action-block-13")]
    public async Task The_mapping_rules_give_the_same_mistakes_from_the_server_and_the_validator_offline()
    {
        var scene = await SceneAsync();
        const string Bad =
            """{"description":"d","firstStep":"run","steps":{"run":{"action":"checkServiceability","with":{"address":"customer.address","nope":"customer.address"},"results":{"serviceable":"customer.address"},"onFailure":"ghost"}}}""";
        var facts = (await scene.Facts.List()).Select(fact => new GlobalFact
        {
            Name = fact.Name,
            Type = fact.Type,
            Description = fact.Description,
            WritableBy = fact.WritableBy,
            Values = fact.Values,
            MaxLength = fact.MaxLength,
        });
        var action = await scene.Actions.Show("checkServiceability");
        var document = $$"""
            {"formatVersion":2,"facts":{{JsonSerializer.Serialize(facts, JsonSerializerOptions.Web)}},
             "actions":[{"id":"checkServiceability","description":"Checks.","parameters":{{action.GetProperty(
                "parameters"
            ).GetRawText()}},"results":{{action.GetProperty("results").GetRawText()}},"implementation":{{action.GetProperty(
                "implementation"
            ).GetRawText()}}}],
             "procedures":[{{Bad.Replace("{\"description\"", "{\"id\":\"orderService\",\"description\"", StringComparison.Ordinal)}}]}
            """;
        var offline = DefinitionValidator.ValidateJson(document, ValidationStage.Save);

        var refused = await FactApi.MistakesOf(await scene.PutProcedure("orderService", Bad));

        Assert.NotEmpty(offline);
        Assert.Equal(
            offline.Select(mistake => (mistake.Code, mistake.Path, mistake.Message, mistake.Fix)).Order(),
            refused
                .Select(mistake =>
                    (
                        mistake.GetProperty("code").GetString() ?? "",
                        mistake.GetProperty("path").GetString() ?? "",
                        mistake.GetProperty("message").GetString() ?? "",
                        mistake.GetProperty("fix").GetString() ?? ""
                    )
                )
                .Order()
        );
    }

    [Fact]
    [Trait("criterion", "AC-action-block-17")]
    public async Task The_uses_of_a_procedure_list_the_action_and_each_mapped_fact_with_the_field_holding_each()
    {
        var scene = await SceneAsync();
        (await scene.PutProcedure("orderService", Order)).EnsureSuccessStatusCode();

        var usage = await ActionApi.Json(
            await scene.Client.GetAsync($"/api/bots/{scene.Bot}/procedures/orderService/usage", TestContext.Current.CancellationToken)
        );

        var uses = usage
            .GetProperty("uses")
            .EnumerateArray()
            .Select(link =>
                $"{link.GetProperty("kind").GetString()} {link.GetProperty("id").GetString()} {link.GetProperty("field").GetString()}"
            )
            .ToList();
        Assert.Contains("action checkServiceability steps.checkAddress.action", uses);
        Assert.Contains("fact customer.address steps.checkAddress.with.address", uses);
        Assert.Contains("fact isServiceable steps.checkAddress.results.serviceable", uses);
    }

    [Fact]
    [Trait("criterion", "AC-action-block-19")]
    public async Task The_view_of_an_action_shows_the_procedures_that_use_it()
    {
        var scene = await SceneAsync();
        (await scene.PutProcedure("orderService", Order)).EnsureSuccessStatusCode();

        var usage = await ActionApi.Json(
            await scene.Client.GetAsync($"/api/bots/{scene.Bot}/actions/checkServiceability/usage", TestContext.Current.CancellationToken)
        );

        Assert.Equal("orderService", (await scene.Actions.Show("checkServiceability")).GetProperty("usedBy")[0].GetString());
        Assert.Equal("orderService", usage.GetProperty("usedBy")[0].GetProperty("id").GetString());
        Assert.Equal("steps.checkAddress.action", usage.GetProperty("usedBy")[0].GetProperty("field").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-16")]
    public async Task An_action_used_in_a_step_cannot_be_deleted_and_the_refusal_lists_the_step()
    {
        var scene = await SceneAsync();
        (await scene.PutProcedure("orderService", Order)).EnsureSuccessStatusCode();

        var refused = await scene.Actions.Delete("checkServiceability");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var mistake = Assert.Single(await FactApi.MistakesOf(refused));
        Assert.Equal(MistakeCodes.ReferenceMissing, mistake.GetProperty("code").GetString());
        Assert.Contains(
            "steps.checkAddress.action of procedure 'orderService'",
            mistake.GetProperty("message").GetString(),
            StringComparison.Ordinal
        );
        Assert.Equal(HttpStatusCode.OK, (await scene.Actions.Get("checkServiceability")).StatusCode);
    }

    [Theory]
    [InlineData(
        """{"description":"Checks.","results":{"serviceable":{"type":"enum","values":["yes","no"],"description":"Served."}},"implementation":{"http":{"method":"POST","url":"https://crm.internal/check"}}}""",
        "parameters.address",
        "removed"
    )]
    [InlineData(
        """{"description":"Checks.","parameters":{"address":{"type":"int","min":1,"max":2,"description":"Where."}},"results":{"serviceable":{"type":"enum","values":["yes","no"],"description":"Served."}},"implementation":{"http":{"method":"POST","url":"https://crm.internal/check"}}}""",
        "parameters.address",
        "changed from text to int"
    )]
    [InlineData(
        """{"description":"Checks.","parameters":{"address":{"type":"text","maxLength":200,"description":"Where."}},"implementation":{"http":{"method":"POST","url":"https://crm.internal/check"}}}""",
        "results.serviceable",
        "removed"
    )]
    [InlineData(
        """{"description":"Checks.","parameters":{"address":{"type":"text","maxLength":200,"description":"Where."}},"results":{"serviceable":{"type":"text","maxLength":5,"description":"Served."}},"implementation":{"http":{"method":"POST","url":"https://crm.internal/check"}}}""",
        "results.serviceable",
        "changed from enum to text"
    )]
    [Trait("criterion", "AC-edit-guard-18")]
    public async Task Removing_or_retyping_a_mapped_parameter_or_result_is_refused_listing_each_step(
        string edited,
        string path,
        string says
    )
    {
        var scene = await SceneAsync();
        (await scene.PutProcedure("orderService", Order)).EnsureSuccessStatusCode();

        var refused = await scene.Actions.Put("checkServiceability", edited);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var mistake = Assert.Single(await FactApi.MistakesOf(refused));
        Assert.Equal(MistakeCodes.ActionFieldInUse, mistake.GetProperty("code").GetString());
        Assert.Equal(path, mistake.GetProperty("path").GetString());
        Assert.Contains(says, mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains("procedure orderService, step checkAddress", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-edit-guard-18")]
    public async Task A_refusal_lists_every_step_that_maps_the_field_in_every_procedure()
    {
        var scene = await SceneAsync();
        (await scene.PutProcedure("orderService", Order)).EnsureSuccessStatusCode();
        (await scene.PutProcedure("recheck", Order.Replace("checkAddress", "again", StringComparison.Ordinal))).EnsureSuccessStatusCode();
        const string withoutResult =
            """{"description":"Checks.","parameters":{"address":{"type":"text","maxLength":200,"description":"Where."}},"implementation":{"http":{"method":"POST","url":"https://crm.internal/check"}}}""";

        var mistake = Assert.Single(await FactApi.MistakesOf(await scene.Actions.Put("checkServiceability", withoutResult)));

        var message = mistake.GetProperty("message").GetString();
        Assert.Contains("procedure orderService, step checkAddress", message, StringComparison.Ordinal);
        Assert.Contains("procedure recheck, step again", message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-edit-guard-3")]
    public async Task A_fact_mapped_to_a_parameter_or_a_result_counts_as_a_use_so_its_type_cannot_change()
    {
        var scene = await SceneAsync();
        (await scene.PutProcedure("orderService", Order)).EnsureSuccessStatusCode();

        var parameter = await scene.Facts.Put("customer.address", FactApi.Floors);
        var result = await scene.Facts.Put("isServiceable", FactApi.Text());

        Assert.Equal(MistakeCodes.FactTypeInUse, Assert.Single(await FactApi.MistakesOf(parameter)).GetProperty("code").GetString());
        Assert.Contains(
            "step checkAddress (with)",
            (await FactApi.MistakesOf(await scene.Facts.Put("customer.address", FactApi.Floors)))[0].GetProperty("message").GetString(),
            StringComparison.Ordinal
        );
        Assert.Contains(MistakeCodes.FactTypeInUse, await FactApi.CodesOf(result));
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-23")]
    public async Task Changing_an_actions_id_rewrites_each_step_and_changing_a_facts_name_rewrites_each_mapping()
    {
        var scene = await SceneAsync();
        (await scene.PutProcedure("orderService", Order)).EnsureSuccessStatusCode();

        var renamedAction = await ActionApi.Json(await scene.Rename("actions", "checkServiceability", "checkAddress"));
        (await scene.Rename("facts", "customer.address", "customer.place")).EnsureSuccessStatusCode();
        (await scene.Rename("facts", "isServiceable", "served")).EnsureSuccessStatusCode();

        Assert.Equal("steps.checkAddress.action", renamedAction.GetProperty("referrers")[0].GetProperty("field").GetString());
        var step = (await scene.Procedure("orderService")).GetProperty("steps").GetProperty("checkAddress");
        Assert.Equal("checkAddress", step.GetProperty("action").GetString());
        Assert.Equal("customer.place", step.GetProperty("with").GetProperty("address").GetString());
        Assert.Equal("served", step.GetProperty("results").GetProperty("serviceable").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await scene.Actions.Get("checkServiceability")).StatusCode);
    }

    private static readonly CodeAction Lookup = new()
    {
        Id = "lookupPolicy",
        Description = "Finds",
        Parameters =
        [
            new CodeActionField
            {
                Name = "address",
                Type = FactTypes.Text,
                Description = "Where",
                MaxLength = 200,
            },
        ],
    };

    private static BotCapabilities Declaring(params CodeAction[] actions) =>
        new()
        {
            FormatVersion = 2,
            Blocks = Block.Names,
            CodeActions = actions,
        };

    private const string UsesLookup =
        """{"description":"d","firstStep":"run","steps":{"run":{"action":"lookupPolicy","with":{"address":"customer.address"},"onFailure":"failed"},"failed":{"say":"Sorry."}}}""";

    private sealed record BotScene(Scene Inner, TestBot Bot)
    {
        public async Task AskAsync() =>
            (
                await Inner.Client.GetAsync($"/api/bots/{Inner.Bot}/declaration", TestContext.Current.CancellationToken)
            ).EnsureSuccessStatusCode();

        public void Answers(params CodeAction[] actions) =>
            Bot.OnDeclaration = context => context.Response.WriteAsJsonAsync(BotDeclaration.Of(Bot.Id, Declaring(actions)));
    }

    private async Task<BotScene> BotSceneAsync()
    {
        _ = database.Pool;
        var client = app.CreateClient();
        disposables.Add(client);
        var id = PushScene.FreshId();
        var bot = await TestBot.StartAsync(id, declares: Declaring(Lookup));
        await PushScene.RegisterAsync(client, id, bot.Address);
        var scene = new Scene(client, new ActionApi(client, id), new FactApi(client, id));
        await scene.AddFactsAsync();
        var result = new BotScene(scene, bot);
        await result.AskAsync();
        (await scene.PutProcedure("usesIt", UsesLookup)).EnsureSuccessStatusCode();
        return result;
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-18")]
    public async Task A_change_made_by_a_declaration_is_not_refused_even_when_a_step_maps_what_changed()
    {
        var scene = await BotSceneAsync();
        await using var _ = scene.Bot;

        scene.Answers(
            Lookup with
            {
                Parameters = [Lookup.Parameters[0] with { Type = FactTypes.WholeNumber, MaxLength = null, Min = 1, Max = 2 }],
            }
        );
        await scene.AskAsync();

        var shown = await scene.Inner.Actions.Show("lookupPolicy");
        Assert.Equal("int", shown.GetProperty("parameters").GetProperty("address").GetProperty("type").GetString());
        Assert.Equal(2, shown.GetProperty("revision").GetInt32());
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-18")]
    public async Task A_declaration_that_breaks_a_mapping_refuses_no_save_and_stops_a_version()
    {
        var scene = await BotSceneAsync();
        await using var _ = scene.Bot;
        (
            await scene.Inner.PutProcedure(
                "conversation",
                """{"description":"b","firstStep":"wait","steps":{"wait":{"listen":{},"next":"answer"},"answer":{"say":"Hi","next":"wait"}}}"""
            )
        ).EnsureSuccessStatusCode();

        scene.Answers(Lookup with { Parameters = [Lookup.Parameters[0] with { MaxLength = 5 }] });
        await scene.AskAsync();

        Assert.Equal(HttpStatusCode.OK, (await scene.Inner.PutProcedure("usesIt", UsesLookup)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await scene.Inner.Facts.Add("another.one", FactApi.Text())).StatusCode);
        var refused = await scene.Inner.Client.PostAsync(
            $"/api/bots/{scene.Inner.Bot}/versions",
            null,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Contains(MistakeCodes.ActionCodeMapping, await FactApi.CodesOf(refused));
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-19")]
    public async Task A_dropped_code_action_in_use_stays_marked_saves_go_on_the_version_names_each_step_and_asking_settles_it()
    {
        var scene = await BotSceneAsync();
        await using var _ = scene.Bot;
        var actions = scene.Inner.Actions;
        Assert.Equal(
            HttpStatusCode.OK,
            (
                await scene.Inner.PutProcedure(
                    "usesIt",
                    """{"description":"d","firstStep":"run","steps":{"run":{"action":"lookupPolicy","with":{"address":"customer.address"},"onFailure":"again"},"again":{"action":"lookupPolicy","with":{"address":"customer.address"},"onFailure":"failed"},"failed":{"say":"Sorry."}}}"""
                )
            ).StatusCode
        );

        scene.Answers();
        await scene.AskAsync();

        var kept = await actions.Show("lookupPolicy");
        Assert.True(kept.GetProperty("noLongerDeclared").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await scene.Inner.Facts.Add("another.one", FactApi.Text())).StatusCode);
        var refused = await scene.Inner.Client.PostAsync(
            $"/api/bots/{scene.Inner.Bot}/versions",
            null,
            TestContext.Current.CancellationToken
        );
        var undeclared = (await FactApi.MistakesOf(refused))
            .Where(mistake => mistake.GetProperty("code").GetString() == MistakeCodes.ActionCodeUndeclared)
            .Select(mistake => mistake.GetProperty("message").GetString() ?? "")
            .ToList();
        var named = Assert.Single(undeclared);
        Assert.Contains("steps.run.action of procedure 'usesIt'", named, StringComparison.Ordinal);
        Assert.Contains("steps.again.action of procedure 'usesIt'", named, StringComparison.Ordinal);

        scene.Answers(Lookup);
        await scene.AskAsync();
        Assert.False((await actions.Show("lookupPolicy")).GetProperty("noLongerDeclared").GetBoolean());

        scene.Answers();
        await scene.AskAsync();
        (
            await scene.Inner.Client.DeleteAsync($"/api/bots/{scene.Inner.Bot}/procedures/usesIt", TestContext.Current.CancellationToken)
        ).EnsureSuccessStatusCode();
        await scene.AskAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await actions.Get("lookupPolicy")).StatusCode);
    }
}
