using Suwagcms.Contracts.Bots;
using Suwagcms.Contracts.Facts;
using Suwagcms.Sdk.Format.Knowledge;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Bots;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Tests.Editing;

public sealed class LoopGuardTests(TestDatabase database)
{
    private static ResourceRef Ref(string id) => new(ResourceKind.Article, id);

    private static Article Article(string id, string? linksTo = null) =>
        new()
        {
            Id = id,
            KnowledgeBase = "faq",
            Title = id,
            Content = linksTo is null ? "Text." : $"See [x](cms://article/{linksTo}).",
            LlmGuide = "Ask first.",
            Description = "What it answers",
        };

    private static Put Write(string id, string? linksTo, int revision) => new(Ref(id), Article(id, linksTo), revision);

    private async Task<(DefinitionEditor Editor, string Bot)> BotWith(params string[] articles)
    {
        var bot = "l" + Guid.NewGuid().ToString("N")[..12];
        var cancellation = TestContext.Current.CancellationToken;
        Assert.True(await BotStore.AddAsync(database.Pool, new Bot(bot, "A bot", "http://bot.internal"), cancellation));
        var editor = new DefinitionEditor(database.Pool, TimeProvider.System);
        var knowledgeBase = new Put(
            new ResourceRef(ResourceKind.KnowledgeBase, "faq"),
            new KnowledgeBase
            {
                Id = "faq",
                Title = "Questions",
                Embedding = "default",
                Results = 5,
            },
            0
        );
        Assert.IsType<EditResult.Saved>(
            await editor.ApplyAsync(bot, [knowledgeBase, .. articles.Select(id => new Put(Ref(id), Article(id), 0))], false, cancellation)
        );
        return (editor, bot);
    }

    [Fact]
    [Trait("criterion", "AC-loop-guard-8")]
    public async Task Of_two_saves_at_the_same_moment_that_each_close_half_a_loop_exactly_one_succeeds_and_the_other_is_refused_with_the_path()
    {
        for (var round = 0; round < 6; round++)
        {
            var (editor, bot) = await BotWith("a", "b");
            var cancellation = TestContext.Current.CancellationToken;

            var results = await Task.WhenAll(
                editor.ApplyAsync(bot, [Write("a", "b", 1)], false, cancellation),
                editor.ApplyAsync(bot, [Write("b", "a", 1)], false, cancellation)
            );

            Assert.Single(results.OfType<EditResult.Saved>());
            var refused = Assert.Single(results.OfType<EditResult.Refused>());
            var mistake = Assert.Single(refused.Mistakes);
            Assert.Equal(MistakeCodes.ReferenceLoop, mistake.Code);
            Assert.Contains("a → b → a", mistake.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [Trait("criterion", "AC-loop-guard-6")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_save_that_removes_one_link_and_adds_another_is_judged_on_its_result_whatever_the_order_of_its_changes(
        bool removeFirst
    )
    {
        var (editor, bot) = await BotWith("a", "b", "c");
        var cancellation = TestContext.Current.CancellationToken;
        Assert.IsType<EditResult.Saved>(await editor.ApplyAsync(bot, [Write("a", "b", 1), Write("b", "c", 1)], false, cancellation));
        Change[] changes = [Write("b", null, 2), Write("c", "a", 1)];

        var result = await editor.ApplyAsync(bot, removeFirst ? changes : [.. changes.Reverse()], false, cancellation);

        Assert.IsType<EditResult.Saved>(result);
        var closing = await editor.ApplyAsync(bot, [Write("b", "c", 3)], false, cancellation);
        Assert.Contains(
            "a → b → c → a",
            Assert.Single(Assert.IsType<EditResult.Refused>(closing).Mistakes).Message,
            StringComparison.Ordinal
        );
    }
}
