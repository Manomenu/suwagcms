using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Suwagcms.Contracts.Actions;
using Suwagcms.Contracts.Availability;
using Suwagcms.Contracts.Facts;
using Suwagcms.Sdk.BotRuntime.Actions;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Format.Procedures.Blocks;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Server.Tests.Actions;
using Suwagcms.Server.Tests.History;

namespace Suwagcms.Server.Tests.Availability;

public sealed class ActionAvailabilityTests(TestDatabase database) : IDisposable
{
    private const string Send = "send";

    private const string Ordering = """
        {"description":"Orders.","firstStep":"sendIt","steps":{
          "sendIt":{"action":"send","onFailure":"failed","next":[{"to":"offer"}]},
          "offer":{"say":"Sent."},
          "failed":{"say":"Sorry."}}}
        """;

    private readonly TestApp app = new();

    public void Dispose() => app.Dispose();

    private async Task<(AvailabilityScene Scene, ActionApi Actions)> SceneAsync(BotCapabilities? declares = null)
    {
        _ = database.Pool;
        var client = app.CreateClient();
        var scene = await AvailabilityScene.StartAsync(client, declares: declares);
        var actions = new ActionApi(client, scene.Id);
        (await actions.Add(Send, ActionApi.Least)).EnsureSuccessStatusCode();
        return (scene, actions);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("criterion", "AC-availability-26")]
    public async Task An_action_turns_off_and_on_and_the_list_the_bot_and_the_history_say_so()
    {
        var (scene, _) = await SceneAsync();
        await using var __ = scene;

        var off = await scene.Turn("off", Send, kind: "action");

        Assert.Equal(AvailabilityDeliveryState.Confirmed, off.Delivery.State);
        Assert.Contains(await ListedAsync(scene), item => item is { Kind: "action", Id: Send, Off: true });
        Assert.True(scene.Bot.Definitions.IsOff(1, ResourceKind.Action, Send));

        await scene.Turn("on", Send, kind: "action");

        Assert.Contains(await ListedAsync(scene), item => item is { Kind: "action", Id: Send, Off: false });
        Assert.False(scene.Bot.Definitions.IsOff(1, ResourceKind.Action, Send));
        var history = await new HistoryScene(scene.Push.Client, scene.Id).Of("actions", Send);
        Assert.Equal(["turned on", "turned off", "added"], history.Entries.Select(entry => entry.Action));
    }

    private static async Task<IReadOnlyList<AvailabilityItem>> ListedAsync(AvailabilityScene scene) =>
        (await scene.View(ask: false)).Resources;

    [Fact]
    [Trait("criterion", "AC-availability-26")]
    public async Task Turning_off_an_action_in_use_names_the_step_that_uses_it()
    {
        var (scene, _) = await SceneAsync();
        await using var __ = scene;
        await PutProcedureAsync(scene);

        var off = await scene.Turn("off", Send, kind: "action");

        Assert.Contains(off.UsedBy, link => link is { Kind: "procedure", Id: "ordering" });
    }

    [Fact]
    [Trait("criterion", "AC-availability-26")]
    [Trait("criterion", "AC-availability-25")]
    public async Task A_renamed_action_keeps_its_setting_also_for_a_version_that_holds_its_old_id()
    {
        var (scene, _) = await SceneAsync();
        await using var __ = scene;
        var version = await scene.Push.CreateAsync();
        await scene.Push.PushAsync(version.Number);
        await scene.Turn("off", Send, kind: "action");

        var renamed = await scene.Push.Client.PostAsJsonAsync(
            $"/api/bots/{scene.Id}/actions/{Send}/rename",
            new { newId = "dispatch" },
            Token
        );
        renamed.EnsureSuccessStatusCode();
        Assert.Equal(AvailabilityDeliveryState.Confirmed, (await scene.Send()).State);

        Assert.Contains(await ListedAsync(scene), item => item is { Kind: "action", Id: "dispatch", Off: true });
        var definitions = scene.Bot.Definitions;
        Assert.True(definitions.IsOff(version.Number, ResourceKind.Action, Send));
        Assert.True(definitions.IsOff(version.Number + 1, ResourceKind.Action, "dispatch"));
        Assert.False(definitions.IsOff(version.Number + 1, ResourceKind.Action, Send));
    }

    [Fact]
    [Trait("criterion", "AC-availability-26")]
    public async Task A_code_action_can_be_turned_off_though_the_rest_of_it_stays_read_only()
    {
        const string code = "lookupPolicy";
        var declares = new BotCapabilities
        {
            FormatVersion = 2,
            Blocks = Block.Names,
            CodeActions = [new CodeAction { Id = code, Description = "Finds" }],
        };
        var (scene, actions) = await SceneAsync(declares);
        await using var __ = scene;
        (await scene.Push.Client.GetAsync($"/api/bots/{scene.Id}/declaration", Token)).EnsureSuccessStatusCode();

        var off = await scene.Turn("off", code, kind: "action");

        Assert.Equal(AvailabilityDeliveryState.Confirmed, off.Delivery.State);
        Assert.True(scene.Bot.Definitions.IsOff(1, ResourceKind.Action, code));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await actions.Put(code, ActionApi.Least)).StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-availability-28")]
    public async Task For_a_step_on_a_turned_off_action_the_sdk_says_it_is_off_on_any_version_and_gives_the_failure_step()
    {
        var (scene, _) = await SceneAsync();
        await using var __ = scene;
        await PutProcedureAsync(scene);
        var older = (await scene.Push.CreateAsync()).Number;
        await scene.Push.PushAsync(older);
        await scene.Push.ChangeAsync();
        var newer = (await scene.Push.CreateAsync()).Number;
        await scene.Push.PushAsync(newer);

        await scene.Turn("off", Send, kind: "action");

        foreach (var number in new[] { older, newer })
        {
            var definition = await scene.Bot.Definitions.GetAsync(number, Token);
            var step = definition.Procedures.Single(procedure => procedure.Id == "ordering").Steps["sendIt"];
            Assert.True(scene.Bot.Definitions.IsOff(number, ResourceKind.Action, step.Action ?? ""));
            Assert.Equal("failed", step.OnFailure);
        }
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-17")]
    public async Task A_turned_off_action_can_still_be_tried_and_the_answer_says_procedures_will_not_run_it()
    {
        var (scene, _) = await SceneAsync();
        await using var __ = scene;
        scene.Bot.OnActionTest = context =>
            context.Response.WriteAsJsonAsync(new ActionOutcome { Outcome = ActionOutcome.Success, ElapsedMs = 1 }, Token);
        var on = await TryAsync(scene);
        await scene.Turn("off", Send, kind: "action");

        var off = await TryAsync(scene);
        var run = await scene.Push.CliAsync("actions", "test", Send, "--yes");

        Assert.Null(on.Note);
        Assert.Equal(("success", "This action is turned off: procedures will not run it until it is turned on."), (off.Outcome, off.Note));
        Assert.Contains("procedures will not run it", run.Output, StringComparison.Ordinal);
    }

    private static async Task<ActionTestAnswer> TryAsync(AvailabilityScene scene)
    {
        using var content = new StringContent("""{"values":{}}""", Encoding.UTF8, "application/json");
        using var response = await scene.Push.Client.PostAsync($"/api/bots/{scene.Id}/actions/{Send}/test", content, Token);
        return await response.Content.ReadFromJsonAsync<ActionTestAnswer>(Token) ?? throw new InvalidOperationException("no answer");
    }

    private static async Task PutProcedureAsync(AvailabilityScene scene)
    {
        using var content = new StringContent(Ordering, Encoding.UTF8, "application/json");
        (await scene.Push.Client.PutAsync($"/api/bots/{scene.Id}/procedures/ordering", content, Token)).EnsureSuccessStatusCode();
    }
}
