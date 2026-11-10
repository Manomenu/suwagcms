using Dapper;
using Suwagcms.Contracts.Versions;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Format.Procedures.Blocks;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Server.Tests.Versions;

public sealed class OldFormatTests(TestDatabase database, ClassServer server) : IClassFixture<ClassServer>
{
    private TestApp App => server.App;

    private async Task<(PushScene Scene, TestBot Bot)> FilledAsync()
    {
        _ = database.Pool;
        var id = PushScene.FreshId();
        var bot = await TestBot.StartAsync(
            id,
            declares: new BotCapabilities
            {
                FormatVersion = BotDefinition.CurrentFormatVersion,
                Blocks = Block.Names,
                EmbeddingProfiles = ["default"],
            }
        );
        var scene = await PushScene.RegisterAsync(App.CreateClient(), id, bot.Address);
        await scene.FillAsync();
        return (scene, bot);
    }

    // Why: this CMS can no longer create a format-1 version, so one left from before is made the only way it can still exist: a row that was written then.
    private async Task MakeOldAsync(string bot, int number)
    {
        await using var connection = await database.Pool.OpenAsync(TestContext.Current.CancellationToken);
        var changed = await connection.ExecuteAsync(
            """
            UPDATE bot_version
            SET format_version = 1, document = replace(document, '"formatVersion":4', '"formatVersion":1')
            WHERE bot_id = @bot AND number = @number AND document LIKE '%"formatVersion":4%'
            """,
            new { bot, number }
        );
        Assert.Equal(1, changed);
    }

    private static void RefusedWithItsFix(PushResult result)
    {
        Assert.Equal(PushOutcome.Failed, result.Outcome);
        var mistake = Assert.Single(result.Mistakes);
        Assert.Equal(MistakeCodes.FormatVersion, mistake.Code);
        Assert.Contains("create a new version from the working state", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-creator-46")]
    public async Task A_version_kept_in_format_1_cannot_be_pushed_and_nothing_reaches_the_bot()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        var version = await scene.CreateAsync();
        await MakeOldAsync(scene.Bot, version.Number);

        var result = await scene.PushAsync(version.Number);

        RefusedWithItsFix(result);
        Assert.Empty((await bot.Store.ReadStateAsync(TestContext.Current.CancellationToken)).Held);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-creator-46")]
    public async Task A_version_kept_in_format_1_cannot_be_rolled_back_to()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        var older = await scene.CreateAsync();
        await scene.PushAsync(older.Number);
        await scene.ChangeAsync();
        var newer = await scene.CreateAsync();
        await scene.PushAsync(newer.Number);
        await MakeOldAsync(scene.Bot, older.Number);

        var result = await scene.PushAsync(older.Number);

        RefusedWithItsFix(result);
        Assert.Equal(newer.Number, (await bot.Store.ReadStateAsync(TestContext.Current.CancellationToken)).Current);
    }
}
