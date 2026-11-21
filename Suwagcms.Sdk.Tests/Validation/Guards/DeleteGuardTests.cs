using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Articles;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.Procedures.Blocks;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;
using Suwagcms.Sdk.Validation.Guards;

namespace Suwagcms.Sdk.Tests.Validation.Guards;

public sealed class DeleteGuardTests
{
    private static readonly ResourceRef Cennik = new(ResourceKind.Article, "cennik");

    private static Article Linking(string id, string content, string llmGuide = "Ask first.") =>
        Samples.Pricing with
        {
            Id = id,
            Content = content,
            LlmGuide = llmGuide,
        };

    private static BotDefinition Bot(params Article[] articles) =>
        new()
        {
            FormatVersion = 5,
            KnowledgeBases = [Samples.Faq],
            Articles = articles,
        };

    private static IReadOnlyList<Mistake> Deleting(BotDefinition after, params ResourceRef[] removed) =>
        DeleteGuard.Reshape(after, removed, DefinitionValidator.Validate(after, ValidationStage.Save));

    [Fact]
    [Trait("criterion", "AC-delete-guard-1")]
    public void A_delete_that_leaves_a_link_behind_is_refused_with_reference_missing_naming_the_resource_deleted()
    {
        var mistake = Assert.Single(Deleting(Bot(Linking("wycena", "See [it](cms://article/cennik).")), Cennik));

        Assert.Equal(MistakeCode.ReferenceMissing, mistake.Code);
        Assert.Equal(Cennik, mistake.Resource);
        Assert.Contains("'cennik'", mistake.Message, StringComparison.Ordinal);
        Assert.Contains("cannot be deleted", mistake.Message, StringComparison.Ordinal);
        Assert.Contains("delete the article again", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-1")]
    public void The_refusal_of_a_delete_points_at_the_id_of_a_resource_and_at_the_name_of_a_fact()
    {
        var article = Assert.Single(Deleting(Bot(Linking("wycena", "See [it](cms://article/cennik).")), Cennik));

        Assert.Equal("id", article.Path);
        Assert.Equal("name", new ResourceRef(ResourceKind.GlobalFact, "customer.city").IdField());
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-2")]
    public void The_refusal_lists_every_referrer_with_the_field_holding_the_reference_once_per_field()
    {
        var after = Bot(
            Linking("wycena", "[a](cms://article/cennik) and again [b](cms://article/cennik)", "[c](cms://article/cennik)"),
            Linking("rabaty", "no link", "[d](cms://article/cennik)"),
            Linking("other", "plain text naming cennik")
        );

        var mistake = Assert.Single(Deleting(after, Cennik));

        Assert.Equal(
            [
                new RelatedResource(new ResourceRef(ResourceKind.Article, "rabaty"), "llmGuide"),
                new RelatedResource(new ResourceRef(ResourceKind.Article, "wycena"), "content"),
                new RelatedResource(new ResourceRef(ResourceKind.Article, "wycena"), "llmGuide"),
            ],
            mistake.Related
        );
        Assert.Contains("llmGuide of article 'rabaty'", mistake.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("other", mistake.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-4")]
    public void A_delete_is_not_refused_once_no_link_is_left() =>
        Assert.Empty(Deleting(Bot(Linking("wycena", "No link any more.")), Cennik));

    [Fact]
    [Trait("criterion", "AC-delete-guard-5")]
    public void A_deleted_referrer_leaves_nothing_that_blocks_its_target() =>
        Assert.Empty(Deleting(Bot(), Cennik, new ResourceRef(ResourceKind.Article, "wycena")));

    [Fact]
    [Trait("criterion", "AC-delete-guard-12")]
    public void A_fact_a_procedure_uses_is_refused_listing_the_procedure_with_the_step_and_field()
    {
        var fact = new ResourceRef(ResourceKind.GlobalFact, "customer.city");
        var after = ProcedureSamples.Bot(ProcedureSamples.All) with
        {
            Facts = [.. ProcedureSamples.Facts.Where(item => item.Name != "customer.city")],
        };

        var mistake = Assert.Single(Deleting(after, fact), mistake => mistake.Resource == fact);

        Assert.Equal(MistakeCode.ReferenceMissing, mistake.Code);
        Assert.All(mistake.Related, related => Assert.Equal(new ResourceRef(ResourceKind.Procedure, "orderValuation"), related.Resource));
        Assert.Contains(mistake.Related, related => related.Location.StartsWith("steps.ask.", StringComparison.Ordinal));
        Assert.Contains(mistake.Related, related => related.Location.Contains("when.customer.city", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-12")]
    public void A_fact_only_a_say_placeholder_shows_is_refused_listing_the_procedure_with_the_step()
    {
        var fact = new ResourceRef(ResourceKind.GlobalFact, "customer.city");
        var after = ProcedureSamples.Bot(ProcedureSamples.Alone(new ProcedureStep { Say = "We come to {customer.city}." })) with
        {
            Facts = [.. ProcedureSamples.Facts.Where(item => item.Name != "customer.city")],
        };

        var mistake = Assert.Single(Deleting(after, fact), mistake => mistake.Resource == fact);

        Assert.Equal(MistakeCode.ReferenceMissing, mistake.Code);
        Assert.Equal(new RelatedResource(new ResourceRef(ResourceKind.Procedure, "p"), "steps.only.say"), Assert.Single(mistake.Related));
        Assert.Contains("steps.only.say of procedure 'p'", mistake.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-13")]
    public void A_procedure_another_one_starts_is_refused_listing_the_starter_with_the_step()
    {
        var target = new ResourceRef(ResourceKind.Procedure, "documentsList");
        var after = ProcedureSamples.Bot(ProcedureSamples.WithStep("get", new ProcedureStep { Start = "documentsList" }));

        var mistake = Assert.Single(Deleting(after, target));

        Assert.Equal(MistakeCode.ReferenceMissing, mistake.Code);
        Assert.Equal(target, mistake.Resource);
        Assert.Equal(
            new RelatedResource(new ResourceRef(ResourceKind.Procedure, "orderValuation"), "steps.get.start"),
            Assert.Single(mistake.Related)
        );
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-17")]
    public void An_article_a_knowledge_entry_names_is_refused_listing_each_instruction_that_names_it()
    {
        IReadOnlyList<KnowledgeEntry> knowledge = [new KnowledgeEntry { KnowledgeBase = "faq", Articles = ["cennik"] }];
        var tell = new ProcedureStep { Inform = new InformArguments { Knowledge = knowledge } };
        var once = ProcedureSamples.WithStep("tell", tell);
        var procedure = once with { Steps = new Dictionary<string, ProcedureStep>(once.Steps) { ["tellAgain"] = tell } };

        var mistake = Assert.Single(Deleting(ProcedureSamples.Bot(procedure) with { KnowledgeBases = [Samples.Faq] }, Cennik));

        Assert.Equal(Cennik, mistake.Resource);
        Assert.Equal(
            ["steps.tell.inform.knowledge[0].articles[0]", "steps.tellAgain.inform.knowledge[0].articles[0]"],
            mistake.Related.Select(related => related.Location)
        );
    }

    [Fact]
    [Trait("criterion", "AC-article-links-7")]
    public void A_typo_in_a_link_is_still_worded_for_the_referrer_not_as_a_delete()
    {
        var mistake = Assert.Single(Deleting(Bot(Linking("wycena", "[x](cms://article/cenik)")), Cennik));

        Assert.Equal(new ResourceRef(ResourceKind.Article, "wycena"), mistake.Resource);
        Assert.Empty(mistake.Related);
    }
}
