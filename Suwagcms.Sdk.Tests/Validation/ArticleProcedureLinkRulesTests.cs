using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation;

public sealed class ArticleProcedureLinkRulesTests
{
    private const string Link = "[the valuation](cms://procedure/orderValuation)";

    private static Article Holding(string content = "Text.", string guide = "Ask first.") =>
        Samples.Pricing with
        {
            Id = "wycena",
            KnowledgeBase = "faq",
            Content = content,
            LlmGuide = guide,
        };

    private static BotDefinition Bot(Article article, params Procedure[] procedures) =>
        new()
        {
            FormatVersion = 1,
            KnowledgeBases = [Samples.Faq],
            Articles = [article],
            Procedures = [Samples.Procedure(Procedure.ConversationId), Samples.Procedure("orderValuation"), .. procedures],
        };

    private static IReadOnlyList<Mistake> Save(Article article, params Procedure[] procedures) =>
        DefinitionValidator.Validate(Bot(article, procedures), ValidationStage.Save);

    [Fact]
    [Trait("criterion", "AC-article-procedure-links-3")]
    public void A_link_to_a_procedure_in_content_is_refused_with_the_fix()
    {
        var mistake = Assert.Single(Save(Holding(content: $"Start {Link}.")));

        Assert.Equal(MistakeCodes.LinkProcedureNotInGuide, mistake.Code);
        Assert.Equal("content", mistake.Path);
        Assert.Contains("links to procedure 'orderValuation'", mistake.Message, StringComparison.Ordinal);
        Assert.Equal("Move the link to llmGuide.", mistake.Fix);
    }

    [Fact]
    [Trait("criterion", "AC-article-procedure-links-4")]
    public void A_procedure_this_bot_does_not_have_is_refused_naming_the_field_and_the_id()
    {
        var mistake = Assert.Single(Save(Holding(guide: "Start [it](cms://procedure/orderValuatoin).")));

        Assert.Equal(MistakeCodes.ReferenceMissing, mistake.Code);
        Assert.Equal("llmGuide", mistake.Path);
        Assert.Contains(
            "llmGuide of 'wycena' links to procedure 'orderValuatoin', which this bot does not have",
            mistake.Message,
            StringComparison.Ordinal
        );
        Assert.Contains("suwagcms procedures list", mistake.Fix, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("criterion", "AC-article-procedure-links-5")]
    [InlineData("Start [it](cms://procedure/).")]
    [InlineData("Start [it](cms://procedure/two/parts).")]
    [InlineData("Start [it](cms://procedure/with-dash).")]
    public void A_malformed_target_is_refused_giving_the_right_form(string guide)
    {
        var mistake = Assert.Single(Save(Holding(guide: guide)));

        Assert.Equal(MistakeCodes.ReferenceMalformed, mistake.Code);
        Assert.Contains("[label](cms://procedure/<id>)", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-article-procedure-links-6")]
    public void A_link_to_conversation_is_refused_saying_it_cannot_be_started()
    {
        var mistake = Assert.Single(Save(Holding(guide: "Go [back](cms://procedure/conversation).")));

        Assert.Equal(MistakeCodes.LinkProcedureConversation, mistake.Code);
        Assert.Contains("cannot be started", mistake.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-article-procedure-links-7")]
    public void A_link_to_a_procedure_with_version_mistakes_is_accepted_together_with_a_link_to_an_article()
    {
        var unfinished = Samples.Procedure("orderValuation") with { FirstStep = null };

        var definition = Bot(Holding(guide: $"Start {Link} after [this](cms://article/wycenaTwo).")) with
        {
            Articles = [Holding(guide: $"Start {Link} after [this](cms://article/wycenaTwo)."), Holding() with { Id = "wycenaTwo" }],
            Procedures = [Samples.Procedure(Procedure.ConversationId), unfinished],
        };

        Assert.Empty(DefinitionValidator.Validate(definition, ValidationStage.Save));
    }

    [Theory]
    [Trait("criterion", "AC-article-procedure-links-8")]
    [InlineData("A code span `[x](cms://procedure/nope)` is text.")]
    [InlineData("```\n[x](cms://procedure/nope)\n```")]
    [InlineData("Start the valuation, or cms://procedure/nope as plain words.")]
    public void Code_and_plain_words_are_not_references_and_are_not_refused(string text)
    {
        var article = Holding(content: text, guide: text);

        Assert.Empty(Save(article));
        Assert.DoesNotContain(ReferenceWalker.From(article), reference => reference.To.Kind == ResourceKind.Procedure);
    }

    [Fact]
    [Trait("criterion", "AC-article-procedure-links-8")]
    public void An_address_in_a_description_or_a_title_is_not_a_reference_and_is_not_refused()
    {
        var article = Holding() with { Title = "See [x](cms://procedure/nope)", Description = "See [x](cms://procedure/nope)" };

        Assert.Empty(Save(article));
        Assert.DoesNotContain(ReferenceWalker.From(article), reference => reference.To.Kind == ResourceKind.Procedure);
    }

    [Fact]
    [Trait("criterion", "AC-article-procedure-links-12")]
    public void In_a_version_a_link_to_a_procedure_keeps_the_label_as_written_and_only_links_to_articles_get_the_current_title()
    {
        var article = Holding(guide: $"Start {Link}, see [Old](cms://article/orderValuation).");

        var labelled = ArticleLinkLabels.WithCurrentTitles(article, new Dictionary<string, string> { ["orderValuation"] = "New" });

        Assert.Equal($"Start {Link}, see [New](cms://article/orderValuation).", labelled.LlmGuide);
    }

    [Fact]
    public void The_walker_yields_a_guide_link_with_its_field_and_no_edge_to_conversation()
    {
        var references = ReferenceWalker.From(Holding(guide: $"{Link} [back](cms://procedure/conversation)")).ToList();

        var reference = Assert.Single(references, found => found.To.Kind == ResourceKind.Procedure);
        Assert.Equal(("llmGuide", "orderValuation"), (reference.Path, reference.To.Id));
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-18")]
    public void A_procedure_named_in_a_guide_cannot_be_deleted()
    {
        var before = Bot(Holding(guide: $"Start {Link}."));
        var after = before with { Procedures = [.. before.Procedures.Where(procedure => procedure.Id != "orderValuation")] };
        var target = new ResourceRef(ResourceKind.Procedure, "orderValuation");

        var mistake = Assert.Single(DeleteGuard.Reshape(after, [target], DefinitionValidator.Validate(after, ValidationStage.Save)));

        Assert.Equal(MistakeCodes.ReferenceMissing, mistake.Code);
        Assert.Equal(target, mistake.Resource);
        Assert.Equal(new RelatedResource(new ResourceRef(ResourceKind.Article, "wycena"), "llmGuide"), Assert.Single(mistake.Related));
    }

    [Fact]
    [Trait("criterion", "AC-loop-guard-14")]
    public void A_loop_from_a_guide_through_a_procedure_back_to_the_article_is_refused()
    {
        var back = Samples.Procedure("orderValuation") with
        {
            WhenStalled = new StallInstruction
            {
                Instruction = "Say so.",
                Context = new InstructionContext { Articles = ["wycena"] },
            },
        };

        var definition = Bot(Holding(guide: $"Start {Link}.")) with { Procedures = [Samples.Procedure(Procedure.ConversationId), back] };

        var mistake = Assert.Single(DefinitionValidator.Validate(definition, ValidationStage.Save));

        Assert.Equal(MistakeCodes.ReferenceLoop, mistake.Code);
        Assert.Contains("wycena → orderValuation → wycena", mistake.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-loop-guard-15")]
    public void A_loop_article_procedure_article_through_a_context_of_an_inform_is_refused()
    {
        var back = Samples.Procedure("orderValuation") with
        {
            FirstStep = "tell",
            Steps = new Dictionary<string, ProcedureStep>
            {
                ["tell"] = new()
                {
                    Inform = new InformArguments
                    {
                        Instruction = "Tell it.",
                        Context = new InstructionContext { Articles = ["wycena"] },
                        Points = new Dictionary<string, string?> { ["one"] = "Text." },
                    },
                },
            },
        };

        var definition = Bot(Holding(guide: $"Start {Link}.")) with { Procedures = [Samples.Procedure(Procedure.ConversationId), back] };

        var mistake = Assert.Single(DefinitionValidator.Validate(definition, ValidationStage.Save));

        Assert.Equal(MistakeCodes.ReferenceLoop, mistake.Code);
        Assert.Equal("llmGuide", mistake.Path);
        Assert.Equal("steps.tell.inform.context.articles[0]", Assert.Single(mistake.Related).Location);
    }
}
