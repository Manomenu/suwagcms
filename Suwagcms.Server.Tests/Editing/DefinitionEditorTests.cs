using Dapper;
using Suwagcms.Contracts.Bots;
using Suwagcms.Contracts.Facts;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Server.Bots;
using Suwagcms.Server.Editing;
using Row = (string PublicId, int Revision, string Body, System.DateTimeOffset UpdatedAt);

namespace Suwagcms.Server.Tests.Editing;

public sealed class DefinitionEditorTests(TestDatabase database)
{
    private static GlobalFact Fact(string name, string description = "A fact", int maxLength = 10) =>
        new()
        {
            Name = name,
            Type = FactTypes.Text,
            Description = description,
            MaxLength = maxLength,
        };

    private static ResourceRef Ref(string name) => new(ResourceKind.GlobalFact, name);

    private static Put Add(string name, string description = "A fact") => new(Ref(name), Fact(name, description), 0);

    private async Task<(DefinitionEditor Editor, string Bot)> NewBot()
    {
        var bot = "e" + Guid.NewGuid().ToString("N")[..12];
        Assert.True(
            await BotStore.AddAsync(database.Pool, new Bot(bot, "A bot", "http://bot.internal"), TestContext.Current.CancellationToken)
        );
        return (new DefinitionEditor(database.Pool, TimeProvider.System), bot);
    }

    private async Task<List<Row>> Rows(string bot)
    {
        await using var connection = await database.Pool.OpenAsync(TestContext.Current.CancellationToken);
        var rows = await connection.QueryAsync<Row>(
            "SELECT public_id AS PublicId, revision AS Revision, body::text AS Body, updated_at AS UpdatedAt FROM resource WHERE bot_id = @bot ORDER BY public_id",
            new { bot }
        );
        return [.. rows];
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-12")]
    public async Task Two_concurrent_saves_with_the_same_expected_revision_let_exactly_one_win()
    {
        var (editor, bot) = await NewBot();
        await editor.ApplyAsync(bot, [Add("customer.city", "Original")], false, TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(
            Enumerable
                .Range(0, 8)
                .Select(number =>
                    editor.ApplyAsync(
                        bot,
                        [new Put(Ref("customer.city"), Fact("customer.city", $"Writer {number}"), 1)],
                        false,
                        TestContext.Current.CancellationToken
                    )
                )
        );

        var winner = Assert.Single(results.OfType<EditResult.Saved>());
        Assert.Equal(7, results.OfType<EditResult.Stale>().Count());
        Assert.Equal(2, winner.Revisions[Ref("customer.city")]);
        var (_, revision, _, _) = Assert.Single(await Rows(bot));
        Assert.Equal(2, revision);
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-12")]
    public async Task Saves_of_different_names_at_the_same_moment_are_all_kept_each_checked_against_the_state_before_it()
    {
        var (editor, bot) = await NewBot();

        var results = await Task.WhenAll(
            Enumerable
                .Range(0, 8)
                .Select(number => editor.ApplyAsync(bot, [Add($"fact{number}")], false, TestContext.Current.CancellationToken))
        );

        Assert.All(results, result => Assert.IsType<EditResult.Saved>(result));
        Assert.Equal(8, (await Rows(bot)).Count);
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-10")]
    public async Task A_dry_run_answers_as_the_real_save_does_and_writes_nothing()
    {
        var (editor, bot) = await NewBot();
        await editor.ApplyAsync(bot, [Add("customer.city", "Before")], false, TestContext.Current.CancellationToken);
        var before = await Rows(bot);
        Change[] changes = [new Put(Ref("customer.city"), Fact("customer.city", "After"), 1), Add("order.kind")];

        var dry = Assert.IsType<EditResult.Saved>(await editor.ApplyAsync(bot, changes, true, TestContext.Current.CancellationToken));

        Assert.False(dry.Written);
        Assert.Equal(before, await Rows(bot));
        var real = Assert.IsType<EditResult.Saved>(await editor.ApplyAsync(bot, changes, false, TestContext.Current.CancellationToken));
        Assert.True(real.Written);
        Assert.Equal(real.Revisions, dry.Revisions);
        Assert.Equal(2, (await Rows(bot)).Count);
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-10")]
    public async Task A_dry_run_of_a_delete_keeps_the_row_and_a_real_delete_removes_it()
    {
        var (editor, bot) = await NewBot();
        await editor.ApplyAsync(bot, [Add("customer.city")], false, TestContext.Current.CancellationToken);

        await editor.ApplyAsync(bot, [new Remove(Ref("customer.city"), null)], true, TestContext.Current.CancellationToken);
        Assert.Single(await Rows(bot));

        await editor.ApplyAsync(bot, [new Remove(Ref("customer.city"), null)], false, TestContext.Current.CancellationToken);
        Assert.Empty(await Rows(bot));
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-5")]
    public async Task A_refused_list_of_changes_leaves_no_trace_even_for_the_changes_that_were_fine()
    {
        var (editor, bot) = await NewBot();
        await editor.ApplyAsync(bot, [Add("customer.city", "Before")], false, TestContext.Current.CancellationToken);
        var before = await Rows(bot);

        var result = await editor.ApplyAsync(
            bot,
            [new Put(Ref("customer.city"), Fact("customer.city", "After"), 1), Add("good.fact"), Add("Bad Name")],
            false,
            TestContext.Current.CancellationToken
        );

        var refused = Assert.IsType<EditResult.Refused>(result);
        Assert.Equal(["fact.name.invalid"], refused.Mistakes.Select(mistake => mistake.Code));
        Assert.Equal(before, await Rows(bot));
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-11")]
    public async Task A_stale_expected_revision_is_refused_before_the_definition_is_checked_and_nothing_is_written()
    {
        var (editor, bot) = await NewBot();
        await editor.ApplyAsync(bot, [Add("customer.city")], false, TestContext.Current.CancellationToken);
        var before = await Rows(bot);

        var result = await editor.ApplyAsync(
            bot,
            [Add("good.fact"), new Put(Ref("customer.city"), Fact("customer.city", ""), 7)],
            false,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(new EditResult.Stale(Ref("customer.city"), 7, 1), result);
        Assert.Equal(before, await Rows(bot));
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-13")]
    public async Task The_same_name_in_two_bots_is_two_resources_with_their_own_revisions()
    {
        var (editor, first) = await NewBot();
        var (_, second) = await NewBot();

        await editor.ApplyAsync(first, [Add("customer.city")], false, TestContext.Current.CancellationToken);
        await editor.ApplyAsync(
            first,
            [new Put(Ref("customer.city"), Fact("customer.city", "Edited"), null)],
            false,
            TestContext.Current.CancellationToken
        );
        await editor.ApplyAsync(second, [Add("customer.city")], false, TestContext.Current.CancellationToken);

        Assert.Equal(2, Assert.Single(await Rows(first)).Revision);
        Assert.Equal(1, Assert.Single(await Rows(second)).Revision);
    }
}
