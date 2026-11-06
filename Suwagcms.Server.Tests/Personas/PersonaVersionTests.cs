using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Suwagcms.Contracts.Bots;
using Suwagcms.Contracts.Facts;
using Suwagcms.Contracts.Versions;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Format.Procedures.Blocks;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.Versions;

namespace Suwagcms.Server.Tests.Personas;

public sealed class PersonaVersionTests(TestDatabase database) : IDisposable
{
    private readonly TestApp app = new();

    public void Dispose() => app.Dispose();

    private async Task<(VersionApi Versions, PersonaApi Persona)> ReadyBot()
    {
        _ = database.Pool;
        var client = app.CreateClient();
        var versions = await (await VersionApi.NewBotAsync(client)).Ready();
        return (versions, new PersonaApi(client, versions.Bot));
    }

    private async Task<(PushScene Scene, TestBot Bot, PersonaApi Persona)> DeclaringBot(string? persona)
    {
        _ = database.Pool;
        var client = app.CreateClient();
        var id = PushScene.FreshId();
        var bot = await TestBot.StartAsync(
            id,
            declares: new BotCapabilities
            {
                FormatVersion = 1,
                Blocks = Block.Names,
                Persona = persona,
            }
        );
        var scene = await PushScene.RegisterAsync(client, id, bot.Address);
        await scene.FillAsync();
        Assert.True(
            (
                await client.GetAsync(new Uri($"/api/bots/{id}/declaration", UriKind.Relative), TestContext.Current.CancellationToken)
            ).IsSuccessStatusCode
        );
        return (scene, bot, new PersonaApi(client, id));
    }

    private static async Task<DeclarationStatus> DeclarationOf(PushScene scene) =>
        await scene.Client.GetFromJsonAsync<DeclarationStatus>($"/api/bots/{scene.Bot}/declaration", TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException("no declaration in the answer");

    private static async Task<VersionComparison> Compare(VersionApi versions, int from, int to) =>
        await versions.Client.GetFromJsonAsync<VersionComparison>(
            $"/api/bots/{versions.Bot}/versions/{from}/diff/{to}",
            TestContext.Current.CancellationToken
        ) ?? throw new InvalidOperationException("no comparison in the answer");

    [Fact]
    [Trait("criterion", "AC-bot-persona-7")]
    public async Task A_version_holds_the_persona_in_the_shape_of_the_format_and_an_empty_part_is_left_out()
    {
        var (versions, persona) = await ReadyBot();
        Assert.True(
            (await persona.Save("""{"personality":"One\nTwo\n","scope":{"inScope":"In\nscope","outOfScope":"  "}}""")).IsSuccessStatusCode
        );

        var created = await versions.Created();
        var held = (await versions.DocumentOf(created.Version.Number)).GetProperty("persona");

        Assert.Equal("""{"personality":"One\nTwo\n","scope":{"inScope":"In\nscope"}}""", held.GetRawText());
        Assert.Equal(1, (await versions.DocumentOf(created.Version.Number)).GetProperty("formatVersion").GetInt32());
    }

    [Theory]
    [Trait("criterion", "AC-bot-persona-7")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_version_of_a_bot_with_an_empty_persona_holds_none(bool savedEmpty)
    {
        var (versions, persona) = await ReadyBot();
        if (savedEmpty)
        {
            Assert.True((await persona.Save("""{"personality":" ","scope":{"inScope":""}}""")).IsSuccessStatusCode);
        }

        var created = await versions.Created();

        Assert.False((await versions.DocumentOf(created.Version.Number)).TryGetProperty("persona", out _));
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-7")]
    public async Task An_older_version_keeps_what_it_had_after_a_later_edit_and_a_clearing_leaves_the_next_version_with_none()
    {
        var (versions, persona) = await ReadyBot();
        await persona.Save("""{"personality":"First"}""");
        var first = (await versions.Created()).Version.Number;
        await persona.Save("""{"personality":"Second"}""");
        var second = (await versions.Created()).Version.Number;
        await persona.Save("{}");
        var third = (await versions.Created()).Version.Number;

        Assert.Equal("First", (await versions.DocumentOf(first)).GetProperty("persona").GetProperty("personality").GetString());
        Assert.Equal("Second", (await versions.DocumentOf(second)).GetProperty("persona").GetProperty("personality").GetString());
        Assert.False((await versions.DocumentOf(third)).TryGetProperty("persona", out _));
        var removed = Assert.Single((await Compare(versions, second, third)).Resources);
        Assert.Equal((ResourceKind.Persona, Difference.Removed), (removed.Kind, removed.Difference));
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-9")]
    public async Task The_comparison_names_the_lines_added_and_removed_in_the_personality_and_each_part_of_the_scope()
    {
        var (versions, persona) = await ReadyBot();
        await persona.Save(
            """{"personality":"Same\nOld voice","scope":{"inScope":"In one","outOfScope":"Out one","whenOutOfScope":"Decline\nOld way"}}"""
        );
        var from = (await versions.Created()).Version.Number;
        await persona.Save(
            """{"personality":"Same\nNew voice","scope":{"inScope":"In two","outOfScope":"Out two","whenOutOfScope":"Decline\nNew way\nAnd more"}}"""
        );
        var to = (await versions.Created()).Version.Number;

        var comparison = await Compare(versions, from, to);

        Assert.Empty(comparison.Definition);
        Assert.Equal(1, comparison.Changed);
        var difference = Assert.Single(comparison.Resources);
        Assert.Equal((ResourceKind.Persona, "persona", Difference.Changed), (difference.Kind, difference.Id, difference.Difference));
        Assert.Equal(
            ["personality", "scope.inScope", "scope.outOfScope", "scope.whenOutOfScope"],
            difference.Fields.Select(field => field.Field)
        );
        string[] Changed(string field, LineChange change)
        {
            return
            [
                .. difference
                    .Fields.Single(item => item.Field == field)
                    .Lines?.Where(line => line.Change == change)
                    .Select(line => line.Text)
                    ?? [],
            ];
        }

        Assert.Equal(["Old voice"], Changed("personality", LineChange.Removed));
        Assert.Equal(["New voice"], Changed("personality", LineChange.Added));
        Assert.Equal(["In one"], Changed("scope.inScope", LineChange.Removed));
        Assert.Equal(["Out two"], Changed("scope.outOfScope", LineChange.Added));
        Assert.Equal(["Old way"], Changed("scope.whenOutOfScope", LineChange.Removed));
        Assert.Equal(["New way", "And more"], Changed("scope.whenOutOfScope", LineChange.Added));
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-9")]
    public async Task A_persona_added_says_its_lines_with_no_empty_line_removed()
    {
        var (versions, persona) = await ReadyBot();
        var from = (await versions.Created()).Version.Number;
        await persona.Save("""{"personality":"One\nTwo"}""");
        var to = (await versions.Created()).Version.Number;

        var difference = Assert.Single((await Compare(versions, from, to)).Resources);

        Assert.Equal(Difference.Added, difference.Difference);
        var field = Assert.Single(difference.Fields);
        Assert.Equal([(LineChange.Added, "One"), (LineChange.Added, "Two")], field.Lines?.Select(line => (line.Change, line.Text)));
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-13")]
    public async Task A_bot_that_requires_a_persona_refuses_a_version_without_one_saves_go_on_and_one_with_it_is_created_and_pushed()
    {
        var (scene, bot, persona) = await DeclaringBot(BotDeclaration.PersonaRequired);
        await using var _ = bot;

        var refused = await scene.Client.PostAsync($"/api/bots/{scene.Bot}/versions", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal([MistakeCodes.PersonaRequired], await FactApi.CodesOf(refused));
        Assert.True((await persona.Save("""{"personality":"You help.\nBriefly."}""")).IsSuccessStatusCode);
        var version = await scene.CreateAsync();
        var result = await scene.PushAsync(version.Number);

        Assert.Equal(PushOutcome.Succeeded, result.Outcome);
        Assert.Equal(
            "You help.\nBriefly.",
            (await bot.Definitions.GetAsync(version.Number, TestContext.Current.CancellationToken)).Persona?.Personality
        );
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-15")]
    public async Task A_bot_that_declares_a_complete_persona_refuses_a_version_with_a_part_empty_and_takes_one_with_all_four()
    {
        var (scene, bot, persona) = await DeclaringBot(BotDeclaration.PersonaComplete);
        await using var _ = bot;
        Assert.True(
            (await persona.Save("""{"personality":"You help.","scope":{"inScope":"In","outOfScope":"Out"}}""")).IsSuccessStatusCode
        );

        var refused = await scene.Client.PostAsync($"/api/bots/{scene.Bot}/versions", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var mistake = Assert.Single(await FactApi.MistakesOf(refused));
        Assert.Equal(MistakeCodes.PersonaIncomplete, mistake.GetProperty("code").GetString());
        Assert.Equal("scope.whenOutOfScope", mistake.GetProperty("path").GetString());
        Assert.True(
            (
                await persona.Save("""{"personality":"You help.","scope":{"inScope":"In","outOfScope":"Out","whenOutOfScope":"Decline"}}""")
            ).IsSuccessStatusCode
        );
        var version = await scene.CreateAsync();

        Assert.Equal(PushOutcome.Succeeded, (await scene.PushAsync(version.Number)).Outcome);
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-15")]
    public async Task A_partial_persona_is_not_listed_as_something_the_bot_cannot_run_and_stays_a_mistake_of_a_version()
    {
        var (scene, bot, persona) = await DeclaringBot(BotDeclaration.PersonaComplete);
        await using var _ = bot;
        Assert.True((await persona.Save("""{"personality":"You help."}""")).IsSuccessStatusCode);

        var status = await DeclarationOf(scene);
        var refused = await scene.Client.PostAsync($"/api/bots/{scene.Bot}/versions", null, TestContext.Current.CancellationToken);

        Assert.Empty(status.CannotRun);
        Assert.Contains(MistakeCodes.PersonaIncomplete, await FactApi.CodesOf(refused));
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-13")]
    public async Task A_missing_persona_is_not_listed_as_something_the_bot_cannot_run_and_stays_a_mistake_of_a_version()
    {
        var (scene, bot, _) = await DeclaringBot(BotDeclaration.PersonaRequired);
        await using var _ = bot;

        var status = await DeclarationOf(scene);
        var refused = await scene.Client.PostAsync($"/api/bots/{scene.Bot}/versions", null, TestContext.Current.CancellationToken);

        Assert.Empty(status.CannotRun);
        Assert.Contains(MistakeCodes.PersonaRequired, await FactApi.CodesOf(refused));
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-13")]
    public async Task A_version_without_a_persona_is_refused_at_a_push_once_the_bot_declares_it_needs_one()
    {
        var (scene, bot, _) = await DeclaringBot(null);
        await using var __ = bot;
        var version = await scene.CreateAsync();
        bot.OnDeclaration = async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(
                    new
                    {
                        bot = scene.Bot,
                        formatVersion = 1,
                        blocks = Block.Names,
                        persona = BotDeclaration.PersonaRequired,
                    }
                ),
                TestContext.Current.CancellationToken
            );
        };

        var result = await scene.PushAsync(version.Number);

        Assert.Equal(PushOutcome.Failed, result.Outcome);
        Assert.Equal(MistakeCodes.PersonaRequired, Assert.Single(result.Mistakes).Code);
        Assert.Null((await scene.StatusAsync()).Has);
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-13")]
    public async Task A_bot_that_does_not_declare_it_takes_a_version_with_or_without_a_persona()
    {
        var (scene, bot, persona) = await DeclaringBot(null);
        await using var _ = bot;

        var without = await scene.CreateAsync();
        Assert.Equal(PushOutcome.Succeeded, (await scene.PushAsync(without.Number)).Outcome);
        await persona.Save("""{"personality":"Voice"}""");
        var with = await scene.CreateAsync();

        Assert.Equal(PushOutcome.Succeeded, (await scene.PushAsync(with.Number)).Outcome);
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-3")]
    public async Task Edit_takes_back_what_show_prints_through_the_real_commands_with_every_text_unchanged()
    {
        var (scene, bot, persona) = await DeclaringBot(null);
        await using var _ = bot;
        var folder = Directory.CreateTempSubdirectory("suwagcms-persona-tests").FullName;
        var file = Path.Combine(folder, "persona.yaml");
        await File.WriteAllTextAsync(
            file,
            "personality: |\n  You help.\n\n    Indented.\nscope:\n  inScope: |-\n    No break at the end\n  whenOutOfScope: |+\n    Several at the end\n\n\n",
            TestContext.Current.CancellationToken
        );

        var edited = await scene.CliAsync("persona", "edit", "--file", file);
        var written = await persona.Read();
        var shown = await scene.CliAsync("persona", "show");
        await File.WriteAllTextAsync(file, shown.Output, TestContext.Current.CancellationToken);
        var again = await scene.CliAsync("persona", "edit", "--file", file);
        var after = await persona.Read();
        Directory.Delete(folder, recursive: true);

        Assert.Equal(0, edited.ExitCode);
        Assert.Equal("You help.\n\n  Indented.\n", written.Personality);
        Assert.Equal("No break at the end", written.Scope?.InScope);
        Assert.Equal("Several at the end\n\n\n", written.Scope?.WhenOutOfScope);
        Assert.Equal(0, again.ExitCode);
        Assert.Equal(written with { Revision = after.Revision }, after);
        Assert.Equal(written.Revision + 1, after.Revision);
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-2")]
    public async Task The_real_show_says_a_persona_never_saved_is_empty()
    {
        var (scene, bot, _) = await DeclaringBot(null);
        await using var __ = bot;

        var shown = await scene.CliAsync("persona", "show");

        Assert.Equal(0, shown.ExitCode);
        Assert.Contains("empty persona", shown.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-7")]
    public async Task The_export_of_a_version_writes_the_persona_as_a_file_of_its_own_and_none_for_a_version_without()
    {
        var (versions, persona) = await ReadyBot();
        var without = (await versions.Created()).Version.Number;
        await persona.Save("""{"personality":"One\nTwo\n"}""");
        var with = (await versions.Created()).Version.Number;

        var exported = await versions.Client.GetFromJsonAsync<VersionExport>(
            $"/api/bots/{versions.Bot}/versions/{with}/export",
            TestContext.Current.CancellationToken
        );
        var plain = await versions.Client.GetFromJsonAsync<VersionExport>(
            $"/api/bots/{versions.Bot}/versions/{without}/export",
            TestContext.Current.CancellationToken
        );

        var file = Assert.Single(exported?.Files ?? [], item => item.Kind == ResourceKind.Persona);
        Assert.Equal("persona.yaml", file.Path);
        Assert.Equal("One\nTwo\n", file.Data.GetProperty("personality").GetString());
        Assert.False(exported?.Files.Single(item => item.Kind is null).Data.TryGetProperty("persona", out _));
        Assert.DoesNotContain(plain?.Files ?? [], item => item.Kind == ResourceKind.Persona);
    }
}
