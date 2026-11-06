using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Suwagcms.Sdk.BotRuntime.Actions;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.Procedures.Blocks;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.Versions;

namespace Suwagcms.Server.Tests.Actions;

public sealed class ActionTestRunTests(TestDatabase database) : IDisposable
{
    private const string Token = "bot-only-value";

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

    private static string ActionJson(string url, string method = "POST", string extra = "") =>
        """
            {"description":"Checks.",
             "parameters":{"address":{"type":"text","maxLength":20,"description":"Where."}},
             "results":{"serviceable":{"type":"enum","values":["yes","no"],"description":"Served."}},
             "implementation":{"http":{"method":"METHOD","url":"URL","headers":{"Authorization":"Bearer {secret:CRM_TOKEN}"}EXTRA}}}
            """.Replace("METHOD", method, StringComparison.Ordinal).Replace("URL", url, StringComparison.Ordinal).Replace(
            "EXTRA",
            extra,
            StringComparison.Ordinal
        );

    private sealed record Scene(HttpClient Client, TestBot Bot, ActionApi Api, FakeService Service) : IAsyncDisposable
    {
        public async Task<HttpResponseMessage> Run(string id = "check", string values = """{"address":"Polna 3"}""")
        {
            using var content = new StringContent($$"""{"values":{{values}}}""", System.Text.Encoding.UTF8, "application/json");
            return await Client.PostAsync($"/api/bots/{Api.Bot}/actions/{id}/test", content, TestContext.Current.CancellationToken);
        }

        public async Task<JsonElement> Answer(string id = "check") => await ActionApi.Json(await Run(id));

        public async ValueTask DisposeAsync()
        {
            await Bot.DisposeAsync();
            await Service.StopAsync();
        }
    }

    private async Task<Scene> SceneAsync(
        HttpClient? over = null,
        BotCapabilities? declares = null,
        Func<string, IReadOnlyDictionary<string, string>, CancellationToken, Task<ActionOutcome>>? runCode = null,
        string? secret = Token
    )
    {
        _ = database.Pool;
        var client = over ?? app.CreateClient();
        disposables.Add(client);
        var id = PushScene.FreshId();
        var bot = await TestBot.StartAsync(
            id,
            declares: declares,
            findSecret: name => name == "CRM_TOKEN" ? secret : null,
            runCode: runCode
        );
        await PushScene.RegisterAsync(client, id, bot.Address);
        var service = await FakeService.StartAsync();
        var api = new ActionApi(client, id);
        (await api.Add("check", ActionJson(service.Address))).EnsureSuccessStatusCode();
        return new Scene(client, bot, api, service);
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-1")]
    public async Task A_run_shows_success_with_the_results_the_status_and_the_time_taken()
    {
        await using var scene = await SceneAsync();

        var answer = await scene.Answer();

        Assert.Equal("success", answer.GetProperty("outcome").GetString());
        Assert.Equal("yes", answer.GetProperty("results").GetProperty("serviceable").GetString());
        Assert.Equal(200, answer.GetProperty("status").GetInt32());
        Assert.True(answer.GetProperty("elapsedMs").GetInt64() >= 0);
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-2")]
    public async Task The_request_reaches_the_service_from_the_bot_with_the_secret_from_the_bots_environment()
    {
        await using var scene = await SceneAsync();
        var before = scene.Bot.Requests;

        await scene.Answer();

        Assert.Equal(before + 1, scene.Bot.Requests);
        var (authorization, body) = Assert.Single(scene.Service.Received);
        Assert.Equal($"Bearer {Token}", authorization);
        Assert.Contains("Polna 3", body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-3")]
    public async Task An_action_never_in_a_version_can_be_run_and_the_bots_version_stays_as_it_was()
    {
        await using var scene = await SceneAsync();
        var state = await TestBot.StateAsync(scene.Bot.Address);

        Assert.Equal("success", (await scene.Answer()).GetProperty("outcome").GetString());

        Assert.Equal(state, await TestBot.StateAsync(scene.Bot.Address));
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-4")]
    public async Task A_failure_answer_shows_the_reason_the_status_the_time_and_the_start_of_the_answer()
    {
        await using var scene = await SceneAsync();
        scene.Service.Answer = context => context.Response.WriteAsync("""{"status":"ok"}""");

        var missing = await scene.Answer();

        Assert.Equal("failure", missing.GetProperty("outcome").GetString());
        Assert.Contains("result 'serviceable' is missing", missing.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.Equal(200, missing.GetProperty("status").GetInt32());
        Assert.Equal("""{"status":"ok"}""", missing.GetProperty("answerStart").GetString());

        scene.Service.Answer = context =>
        {
            context.Response.StatusCode = 503;
            return context.Response.WriteAsync("down");
        };
        var down = await scene.Answer();
        Assert.Equal(503, down.GetProperty("status").GetInt32());
        Assert.Equal("down", down.GetProperty("answerStart").GetString());
        Assert.True(down.GetProperty("elapsedMs").GetInt64() >= 0);
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-4")]
    public async Task A_non_JSON_answer_a_result_that_does_not_fit_and_a_timeout_each_show_their_reason_and_the_time_taken()
    {
        await using var scene = await SceneAsync();
        scene.Service.Answer = context => context.Response.WriteAsync("<html>oops</html>");

        var notJson = await scene.Answer();

        Assert.Equal("failure", notJson.GetProperty("outcome").GetString());
        Assert.Contains("it is not a JSON object", notJson.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.Equal(200, notJson.GetProperty("status").GetInt32());
        Assert.Equal("<html>oops</html>", notJson.GetProperty("answerStart").GetString());
        Assert.True(notJson.GetProperty("elapsedMs").GetInt64() >= 0);

        scene.Service.Answer = context => context.Response.WriteAsync("""{"serviceable":"maybe"}""");

        var unfit = await scene.Answer();

        Assert.Equal("failure", unfit.GetProperty("outcome").GetString());
        Assert.Contains("result 'serviceable' does not fit", unfit.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.Equal("""{"serviceable":"maybe"}""", unfit.GetProperty("answerStart").GetString());
        Assert.True(unfit.GetProperty("elapsedMs").GetInt64() >= 0);

        (await scene.Api.Put("check", ActionJson(scene.Service.Address, extra: ""","timeoutSeconds":1"""))).EnsureSuccessStatusCode();
        scene.Service.Answer = context => Task.Delay(TimeSpan.FromSeconds(5), context.RequestAborted);

        var slow = await scene.Answer();

        Assert.Equal("failure", slow.GetProperty("outcome").GetString());
        Assert.Contains("within the time limit of 1 seconds", slow.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.InRange(slow.GetProperty("elapsedMs").GetInt64(), 900, 4000);
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-4")]
    public async Task A_secret_not_found_and_no_connection_are_failures_with_their_reasons()
    {
        await using var noSecret = await SceneAsync(secret: null);
        var absent = await noSecret.Answer();
        Assert.Equal("failure", absent.GetProperty("outcome").GetString());
        Assert.Contains(
            "CRM_TOKEN was not found in the bot's environment",
            absent.GetProperty("reason").GetString(),
            StringComparison.Ordinal
        );
        Assert.Empty(noSecret.Service.Received);

        await using var gone = await SceneAsync();
        await gone.Service.StopAsync();
        var refused = await gone.Answer();
        Assert.Equal("failure", refused.GetProperty("outcome").GetString());
        Assert.Contains("No connection", refused.GetProperty("reason").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-5")]
    public async Task A_value_that_does_not_fit_and_a_parameter_left_out_are_reported_at_once_and_nothing_reaches_the_bot()
    {
        await using var scene = await SceneAsync();
        (
            await scene.Api.Put(
                "check",
                ActionJson(scene.Service.Address)
                    .Replace(
                        "\"parameters\":{",
                        "\"parameters\":{\"floors\":{\"type\":\"int\",\"min\":1,\"max\":2,\"description\":\"F\"},",
                        StringComparison.Ordinal
                    )
            )
        ).EnsureSuccessStatusCode();
        var before = scene.Bot.Requests;

        var refused = await scene.Run(values: """{"floors":"9"}""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var mistakes = await FactApi.MistakesOf(refused);
        Assert.Equal(
            ["values.address", "values.floors"],
            mistakes.Select(mistake => mistake.GetProperty("path").GetString()).Order(StringComparer.Ordinal)
        );
        Assert.All(mistakes, mistake => Assert.NotEmpty(mistake.GetProperty("fix").GetString() ?? ""));
        Assert.Equal(before, scene.Bot.Requests);
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-6")]
    public async Task With_the_bot_unreachable_the_run_says_so_and_is_told_apart_from_a_failure_of_the_action()
    {
        await using var scene = await SceneAsync();
        await scene.Bot.StopAsync();

        var answer = await scene.Answer();

        Assert.Equal("unreachable", answer.GetProperty("outcome").GetString());
        Assert.Contains("has not been judged", answer.GetProperty("reason").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-6")]
    public async Task With_the_bot_not_answering_in_time_the_run_says_so()
    {
        using var quick = app.WithWebHostBuilder(builder => builder.UseSetting("Push:ActionTestMarginSeconds", "0"));
        await using var scene = await SceneAsync(quick.CreateClient());
        (await scene.Api.Put("check", ActionJson(scene.Service.Address, extra: ""","timeoutSeconds":1"""))).EnsureSuccessStatusCode();
        scene.Bot.OnActionTest = context => Task.Delay(TimeSpan.FromSeconds(20), context.RequestAborted);

        var answer = await scene.Answer();

        Assert.Equal("unreachable", answer.GetProperty("outcome").GetString());
        Assert.Contains("did not answer within 1 seconds", answer.GetProperty("reason").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-7")]
    public async Task A_bot_without_the_test_route_shows_as_not_offering_test_runs_with_the_fix()
    {
        await using var scene = await SceneAsync();
        scene.Bot.OnActionTest = context =>
        {
            context.Response.StatusCode = 404;
            return Task.CompletedTask;
        };

        var answer = await scene.Answer();

        Assert.Equal("notOffered", answer.GetProperty("outcome").GetString());
        Assert.Contains(
            "does not offer test runs. Update its Suwagcms.Sdk",
            answer.GetProperty("reason").GetString(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-8")]
    public async Task A_secrets_value_appears_nowhere_in_the_answer_even_when_the_service_repeats_it()
    {
        await using var scene = await SceneAsync();
        scene.Service.Answer = context => context.Response.WriteAsync($$"""{"serviceable":"maybe","echo":"{{Token}}"}""");

        var refusedResult = await scene.Run();
        var text = await refusedResult.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(Token, text, StringComparison.Ordinal);
        Assert.Contains("***", text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-9")]
    public async Task A_body_of_more_than_2000_characters_is_cut_with_a_note()
    {
        await using var scene = await SceneAsync();
        scene.Service.Answer = context =>
        {
            context.Response.StatusCode = 500;
            return context.Response.WriteAsync(new string('x', 2500));
        };

        var answer = await scene.Answer();

        Assert.Equal(2000, answer.GetProperty("answerStart").GetString()?.Length);
        Assert.True(answer.GetProperty("answerCut").GetBoolean());
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-10")]
    public async Task A_run_changes_no_resource_or_version_and_leaves_no_record()
    {
        await using var scene = await SceneAsync();
        var path = $"/api/bots/{scene.Api.Bot}/history";
        var history = await scene.Client.GetStringAsync(path, TestContext.Current.CancellationToken);
        var shown = (await scene.Api.Show("check")).GetRawText();
        var versions = await scene.Client.GetStringAsync($"/api/bots/{scene.Api.Bot}/versions", TestContext.Current.CancellationToken);

        await scene.Answer();

        Assert.Equal(history, await scene.Client.GetStringAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal(shown, (await scene.Api.Show("check")).GetRawText());
        Assert.Equal(
            versions,
            await scene.Client.GetStringAsync($"/api/bots/{scene.Api.Bot}/versions", TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-11")]
    public async Task A_save_of_the_same_bot_during_a_run_completes_without_waiting_for_it()
    {
        await using var scene = await SceneAsync();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var called = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        scene.Service.Answer = async context =>
        {
            called.SetResult();
            await release.Task;
            await context.Response.WriteAsync("""{"serviceable":"yes"}""");
        };

        var run = scene.Run();
        await called.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var saved = await scene.Api.Add("another", ActionApi.Least);
        var runStillWaiting = !run.IsCompleted;
        release.SetResult();

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.True(runStillWaiting);
        Assert.Equal("success", (await ActionApi.Json(await run)).GetProperty("outcome").GetString());
    }

    private static readonly CodeAction Lookup = new()
    {
        Id = "lookupPolicy",
        Description = "Finds",
        Parameters =
        [
            new CodeActionField
            {
                Name = "pesel",
                Type = FactTypes.Text,
                Description = "P",
                MaxLength = 11,
            },
        ],
        Results =
        [
            new CodeActionField
            {
                Name = "policy",
                Type = FactTypes.Text,
                Description = "N",
                MaxLength = 5,
            },
        ],
    };

    private static BotCapabilities Declaring() =>
        new()
        {
            FormatVersion = 2,
            Blocks = Block.Names,
            CodeActions = [Lookup],
        };

    [Fact]
    [Trait("criterion", "AC-action-test-run-16")]
    public async Task A_run_of_a_code_action_goes_through_the_bot_and_runs_its_code_with_the_values()
    {
        await using var scene = await SceneAsync(
            declares: Declaring(),
            runCode: (id, values, _) =>
                Task.FromResult(
                    new ActionOutcome
                    {
                        Outcome = ActionOutcome.Success,
                        Results = new Dictionary<string, string> { ["policy"] = $"{id[..2]}{values["pesel"]}" },
                    }
                )
        );
        await scene.Client.GetAsync($"/api/bots/{scene.Api.Bot}/declaration", TestContext.Current.CancellationToken);

        var run = await scene.Run("lookupPolicy", """{"pesel":"123"}""");

        var answer = await ActionApi.Json(run);
        Assert.Equal("success", answer.GetProperty("outcome").GetString());
        Assert.Equal("lo123", answer.GetProperty("results").GetProperty("policy").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-16")]
    public async Task A_bot_with_no_way_to_run_a_code_action_from_the_sdk_says_so()
    {
        await using var scene = await SceneAsync(declares: Declaring());
        await scene.Client.GetAsync($"/api/bots/{scene.Api.Bot}/declaration", TestContext.Current.CancellationToken);

        var answer = await ActionApi.Json(await scene.Run("lookupPolicy", """{"pesel":"123"}"""));

        Assert.Equal("failure", answer.GetProperty("outcome").GetString());
        Assert.Contains("no way to run a code action from the SDK", answer.GetProperty("reason").GetString(), StringComparison.Ordinal);
    }
}
