using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Suwagcms.Contracts.Facts;
using Suwagcms.Contracts.Personas;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Editing;
using Suwagcms.Server.Tests.Facts;

namespace Suwagcms.Server.Tests.Personas;

public sealed class PersonaEndpointsTests(TestDatabase database, ClassServer server) : IClassFixture<ClassServer>
{
    private TestApp App => server.App;

    private async Task<PersonaApi> NewBot()
    {
        _ = database.DataSource;
        return await PersonaApi.NewBotAsync(App.CreateClient());
    }

    private static async Task<PersonaView> ViewOf(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<PersonaView>(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("no persona in the answer");
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-1")]
    public async Task Each_text_reads_back_exactly_as_written_with_its_line_breaks_and_the_spaces_around_them()
    {
        var api = await NewBot();
        const string Written = "  You help.\n\n\tBriefly.  \nLast line\n\n";

        var saved = await ViewOf(
            await api.Save(JsonSerializer.Serialize(new { personality = Written, scope = new { inScope = Written } }))
        );
        var read = await api.Read();

        Assert.Equal(Written, saved.Personality);
        Assert.Equal(Written, read.Personality);
        Assert.Equal(Written, read.Scope?.InScope);
        Assert.Equal(1, read.Revision);
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-2")]
    public async Task A_persona_never_saved_answers_empty_at_revision_0_and_white_space_counts_as_empty()
    {
        var api = await NewBot();

        Assert.Equal(new PersonaView(null, null, 0), await api.Read());

        var saved = await ViewOf(await api.Save("""{"personality":" \n ","scope":{"inScope":"","outOfScope":"\t"}}"""));

        Assert.Null(saved.Personality);
        Assert.Null(saved.Scope);
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-4")]
    public async Task Every_text_above_the_limit_is_refused_at_once_and_nothing_is_saved()
    {
        var api = await NewBot();
        var tooLong = new string('x', 20_001);

        var response = await api.Save(JsonSerializer.Serialize(new { personality = tooLong, scope = new { outOfScope = tooLong } }));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var mistakes = await FactApi.MistakesOf(response);
        Assert.Equal(["personality", "scope.outOfScope"], mistakes.Select(mistake => mistake.GetProperty("at").GetString()));
        Assert.All(mistakes, mistake => Assert.Equal(MistakeCodes.PersonaTextTooLong, mistake.GetProperty("code").GetString()));
        Assert.All(mistakes, mistake => Assert.False(string.IsNullOrEmpty(mistake.GetProperty("fix").GetString())));
        Assert.Equal(new PersonaView(null, null, 0), await api.Read());
    }

    [Theory]
    [Trait("criterion", "AC-bot-persona-5")]
    [InlineData("""{"personalty":"Hi"}""", "personalty", "personalty")]
    [InlineData("""{"scope":{"inScop":"Hi"}}""", "scope.inScop", "inScop")]
    public async Task A_field_the_format_does_not_know_is_refused_naming_it_and_nothing_is_saved(string body, string path, string field)
    {
        var api = await NewBot();

        var response = await api.Save(body);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var mistake = Assert.Single(await FactApi.MistakesOf(response));
        Assert.Equal(StrictBody.UnknownField, mistake.GetProperty("code").GetString());
        Assert.Equal(path, mistake.GetProperty("at").GetString());
        Assert.Contains(field, mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal(0, (await api.Read()).Revision);
    }

    [Theory]
    [Trait("criterion", "AC-bot-persona-6")]
    [InlineData(0)]
    [InlineData(1)]
    public async Task A_save_on_an_old_revision_is_refused_as_out_of_date_with_the_current_one(int old)
    {
        var api = await NewBot();
        await ViewOf(await api.Save(PersonaApi.Whole));
        await ViewOf(await api.Save("""{"personality":"Piotr's"}"""));

        var response = await api.Save("""{"personality":"Marta's"}""", $"?ifRevision={old}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var mistake = Assert.Single(await FactApi.MistakesOf(response));
        Assert.Equal(EditProblems.ResourceStale, mistake.GetProperty("code").GetString());
        Assert.Contains("revision 2", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal("Piotr's", (await api.Read()).Personality);
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-6")]
    public async Task A_dry_run_answers_as_a_real_save_and_changes_nothing()
    {
        var api = await NewBot();
        await ViewOf(await api.Save("""{"personality":"Before"}"""));

        var dry = await ViewOf(await api.Save(PersonaApi.Whole, "?dryRun=true&ifRevision=1"));
        var history = await api.HistoryOfPersona();
        Assert.Equal("Before", (await api.Read()).Personality);
        var stale = await api.Save(PersonaApi.Whole, "?dryRun=true&ifRevision=9");
        var tooLong = await api.Save(JsonSerializer.Serialize(new { personality = new string('x', 20_001) }), "?dryRun=true");
        var real = await ViewOf(await api.Save(PersonaApi.Whole, "?ifRevision=1"));

        Assert.Equal(real, dry);
        Assert.Single(history.Entries);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooLong.StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-10")]
    public async Task Each_change_is_in_the_history_of_the_persona_and_of_the_bot_naming_the_part_and_a_save_with_no_change_adds_nothing()
    {
        var api = await NewBot();
        await ViewOf(await api.Save(PersonaApi.Whole));
        await ViewOf(await api.Save(PersonaApi.Whole));
        await ViewOf(
            await api.Save(
                """
                {"personality":"You help.\nBriefly.\n","scope":{"inScope":"Valuations\nGreetings","outOfScope":"Legal advice","whenOutOfScope":"Decline\nSay more"}}
                """
            )
        );

        var entries = (await api.HistoryOfPersona()).Entries;

        Assert.Equal(["edited", "added"], entries.Select(entry => entry.Action));
        var change = Assert.Single(entries[0].Changes);
        Assert.Equal("scope.whenOutOfScope", change.Field);
        Assert.Equal("Decline\nOffer help", change.Old?.GetString());
        Assert.Equal("Decline\nSay more", change.New?.GetString());
        Assert.Equal(
            entries.Select(entry => entry.Id),
            (await api.HistoryOfBot()).Entries.Where(entry => entry.Kind == "persona").Select(entry => entry.Id)
        );
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-11")]
    public async Task A_persona_of_one_bot_is_never_seen_or_changed_through_another()
    {
        var first = await NewBot();
        var second = await NewBot();

        await ViewOf(await first.Save(PersonaApi.Whole));

        Assert.Equal(new PersonaView(null, null, 0), await second.Read());
        Assert.Empty((await second.HistoryOfPersona()).Entries);

        await ViewOf(await second.Save("""{"personality":"Another voice"}"""));

        Assert.Equal("You help.\nBriefly.\n", (await first.Read()).Personality);
        Assert.Equal(1, (await first.Read()).Revision);
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-11")]
    public async Task A_bot_that_does_not_exist_has_no_persona_to_show_or_save()
    {
        _ = database.DataSource;
        var client = App.CreateClient();
        var nobody = new PersonaApi(client, "no-such-bot");

        Assert.Equal(HttpStatusCode.NotFound, (await nobody.Save(PersonaApi.Whole)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (
                await client.GetAsync(new Uri("/api/bots/no-such-bot/persona", UriKind.Relative), TestContext.Current.CancellationToken)
            ).StatusCode
        );
    }

    [Theory]
    [Trait("criterion", "AC-bot-persona-14")]
    [InlineData("rename", "/api/bots/{0}/persona/persona/rename")]
    [InlineData("off", "/api/bots/{0}/availability/persona/persona/off")]
    public async Task A_persona_cannot_be_renamed_or_turned_off(string _, string address)
    {
        var api = await NewBot();
        await ViewOf(await api.Save(PersonaApi.Whole));
        using var content = new StringContent("""{"newId":"other"}""", System.Text.Encoding.UTF8, "application/json");

        var response = await App.CreateClient()
            .PostAsync(
                new Uri(string.Format(System.Globalization.CultureInfo.InvariantCulture, address, api.Bot), UriKind.Relative),
                content,
                TestContext.Current.CancellationToken
            );

        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(1, (await api.Read()).Revision);
    }

    [Theory]
    [Trait("criterion", "AC-bot-persona-14")]
    [InlineData("add")]
    [InlineData("delete")]
    public async Task A_file_of_changes_can_only_edit_a_persona(string verb)
    {
        var api = await NewBot();

        var response = await App.CreateClient()
            .PostAsJsonAsync(
                $"/api/bots/{api.Bot}/changes",
                new { changes = new object[] { new Dictionary<string, string> { [verb] = "persona" } } },
                TestContext.Current.CancellationToken
            );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains(
            "never added or deleted",
            Assert.Single(await FactApi.MistakesOf(response)).GetProperty("message").GetString(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-3")]
    public async Task A_file_of_changes_replaces_the_whole_persona_with_its_line_breaks_even_one_never_saved()
    {
        var api = await NewBot();
        var client = App.CreateClient();

        var first = await client.PostAsJsonAsync(
            $"/api/bots/{api.Bot}/changes",
            new
            {
                changes = new object[]
                {
                    new
                    {
                        edit = "persona",
                        personality = "One\nTwo\n",
                        scope = new { inScope = "In" },
                    },
                },
            },
            TestContext.Current.CancellationToken
        );
        var second = await client.PostAsJsonAsync(
            $"/api/bots/{api.Bot}/changes",
            new
            {
                changes = new object[]
                {
                    new
                    {
                        edit = "persona",
                        personality = "Three\n",
                        ifRevision = 1,
                    },
                },
            },
            TestContext.Current.CancellationToken
        );

        Assert.True(first.IsSuccessStatusCode);
        Assert.True(second.IsSuccessStatusCode);
        Assert.Equal(new PersonaView("Three\n", null, 2), await api.Read());
    }
}
