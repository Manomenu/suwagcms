using System.Text.Json;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Articles;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Format.KnowledgeBases;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.Procedures.Blocks;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation.Rules;

public sealed class KnowledgeEntryRulesTests
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly KnowledgeBase Offer = new()
    {
        Id = "offer",
        Title = "Offer",
        Embedding = "default",
        Results = 5,
    };

    private static Article Office(string knowledgeBase = "faq") =>
        Samples.Pricing with
        {
            Id = "officeContact",
            KnowledgeBase = knowledgeBase,
        };

    private static KnowledgeEntry Entry(string knowledgeBase = "faq", string[]? articles = null, string[]? queries = null) =>
        new()
        {
            KnowledgeBase = knowledgeBase,
            Articles = articles,
            SearchQueries = queries,
        };

    private static Procedure InformingWith(params KnowledgeEntry[]? knowledge) =>
        ProcedureSamples.Alone(
            new ProcedureStep
            {
                Inform = new InformArguments
                {
                    Instruction = "Tell it.",
                    Points = new Dictionary<string, string?> { ["office"] = "Where the office is." },
                    Knowledge = knowledge,
                },
            }
        );

    private static BotDefinition Bot(Procedure procedure, params Article[] articles) =>
        new()
        {
            FormatVersion = BotDefinition.CurrentFormatVersion,
            Facts = ProcedureSamples.Facts,
            KnowledgeBases = [Samples.Faq, Offer],
            Articles = articles,
            Procedures = [Samples.Procedure(Procedure.ConversationId), procedure],
        };

    private static IReadOnlyList<Mistake> Check(ValidationStage stage, Procedure procedure, params Article[] articles) =>
        DefinitionValidator.Validate(Bot(procedure, articles), stage);

    [Fact]
    [Trait("criterion", "AC-instruction-context-1")]
    public void An_entry_with_a_base_articles_and_queries_reads_back_as_written_through_the_format()
    {
        var procedure = InformingWith(Entry(articles: ["officeContact"], queries: ["how long a valuation takes"]));

        var back = JsonSerializer.Deserialize<Procedure>(JsonSerializer.Serialize(procedure, Wire), Wire);

        var entry = Assert.Single(back?.Steps["only"].Inform?.Knowledge ?? []);
        Assert.Equal("faq", entry.KnowledgeBase);
        Assert.Equal(["officeContact"], entry.Articles);
        Assert.Equal(["how long a valuation takes"], entry.SearchQueries);
    }

    public static TheoryData<KnowledgeEntry[]?> Harmless =>
        new(null, [], [Entry(articles: ["officeContact"])], [Entry(queries: ["opening hours"])]);

    [Theory]
    [Trait("criterion", "AC-instruction-context-2")]
    [MemberData(nameof(Harmless))]
    public void No_knowledge_an_empty_list_and_an_entry_with_one_key_alone_are_each_accepted_at_every_stage(KnowledgeEntry[]? knowledge) =>
        Assert.Empty(Check(ValidationStage.Version, InformingWith(knowledge), Office()));

    [Fact]
    [Trait("criterion", "AC-instruction-context-3")]
    public void An_article_the_bot_does_not_have_is_refused_naming_where_the_entry_is()
    {
        var mistake = Assert.Single(Check(ValidationStage.Save, InformingWith(Entry(articles: ["officeContacts"])), Office()));

        Assert.Equal(MistakeCodes.ReferenceMissing, mistake.Code);
        Assert.Equal("steps.only.inform.knowledge[0].articles[0]", mistake.Path);
        Assert.Equal("'knowledge' of step 'only' in 'p' names article 'officeContacts', which this bot does not have.", mistake.Message);
        Assert.Contains("'suwagcms articles list' shows them", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-3")]
    public void A_base_the_bot_does_not_have_is_refused_listing_the_bases_it_has()
    {
        var mistake = Assert.Single(Check(ValidationStage.Save, InformingWith(Entry("reports", queries: ["hours"]))));

        Assert.Equal(MistakeCodes.ReferenceMissing, mistake.Code);
        Assert.Equal("steps.only.inform.knowledge[0].knowledgeBase", mistake.Path);
        Assert.Contains("'reports'", mistake.Message, StringComparison.Ordinal);
        Assert.Contains("faq, offer", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-4")]
    public void An_article_id_that_is_not_camelCase_is_refused_with_the_id_to_use_and_not_also_as_missing()
    {
        var mistake = Assert.Single(Check(ValidationStage.Save, InformingWith(Entry(articles: ["Office Contact"]))));

        Assert.Equal(MistakeCodes.ReferenceMalformed, mistake.Code);
        Assert.Contains("'officeContact'", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-4")]
    public void An_entry_with_no_base_is_refused_with_how_to_name_one()
    {
        var mistake = Assert.Single(Check(ValidationStage.Save, InformingWith(Entry("", queries: ["hours"]))));

        Assert.Equal(MistakeCodes.KnowledgeBaseMissing, mistake.Code);
        Assert.Equal("steps.only.inform.knowledge[0].knowledgeBase", mistake.Path);
        Assert.Contains("knowledgeBase", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-4")]
    public void Knowledge_on_a_block_with_no_instruction_is_refused_saying_where_it_belongs()
    {
        const string json = """
            {"formatVersion":5,"procedures":[{"id":"p","description":"d","firstStep":"a","steps":{
              "a":{"say":"Hi","knowledge":[{"knowledgeBase":"faq","articles":["officeContact"]}]}}}]}
            """;

        var mistake = Assert.Single(DefinitionValidator.ValidateJson(json, ValidationStage.Save));

        Assert.Equal(MistakeCodes.StepBlockUnknown, mistake.Code);
        Assert.Equal("steps.a.knowledge", mistake.Path);
        Assert.Contains("inform", mistake.Fix, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("criterion", "AC-instruction-context-5")]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_query_passes_the_save_stage_and_is_a_version_mistake(string query)
    {
        var procedure = InformingWith(Entry(queries: ["opening hours", query]));

        Assert.Empty(Check(ValidationStage.Save, procedure, Office()));

        var mistake = Assert.Single(Check(ValidationStage.Version, procedure, Office()));
        Assert.Equal(MistakeCodes.KnowledgeQueryBlank, mistake.Code);
        Assert.Equal("steps.only.inform.knowledge[0].searchQueries[1]", mistake.Path);
        Assert.Equal("'knowledge' of step 'only' in 'p' has a blank search query.", mistake.Message);
        Assert.Equal("Write the query, or remove it.", mistake.Fix);
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-5")]
    public void An_entry_with_no_articles_and_no_queries_passes_the_save_stage_and_is_a_version_mistake()
    {
        var procedure = InformingWith(Entry(articles: [], queries: []));

        Assert.Empty(Check(ValidationStage.Save, procedure, Office()));

        var mistake = Assert.Single(Check(ValidationStage.Version, procedure, Office()));
        Assert.Equal(MistakeCodes.KnowledgeEntryEmpty, mistake.Code);
        Assert.Equal("steps.only.inform.knowledge[0]", mistake.Path);
        Assert.Contains("remove the entry", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-6")]
    public void A_query_on_a_base_with_no_articles_is_a_version_mistake_that_goes_once_that_base_has_an_article()
    {
        var procedure = InformingWith(Entry(queries: ["opening hours"]));

        Assert.Empty(Check(ValidationStage.Save, procedure));
        Assert.Equal(MistakeCodes.KnowledgeQueryNoArticles, Assert.Single(Check(ValidationStage.Version, procedure)).Code);
        Assert.Equal(MistakeCodes.KnowledgeQueryNoArticles, Assert.Single(Check(ValidationStage.Version, procedure, Office("offer"))).Code);
        Assert.Empty(Check(ValidationStage.Version, procedure, Office()));
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-8")]
    public void The_validator_on_its_own_reports_the_reference_and_version_mistakes_with_the_same_codes_from_json()
    {
        const string missing = """
            {"formatVersion":5,"knowledgeBases":[{"id":"faq","title":"FAQ","embedding":"default","results":5}],"procedures":[{"id":"p","description":"d","firstStep":"a","steps":{
              "a":{"inform":{"instruction":"Say it.","points":{"office":"Where it is."},"knowledge":[{"knowledgeBase":"faq","articles":["ghost"]}]}}}}]}
            """;
        const string unfinished = """
            {"formatVersion":5,"knowledgeBases":[{"id":"faq","title":"FAQ","embedding":"default","results":5}],"procedures":[{"id":"conversation","description":"d","firstStep":"a","steps":{
              "a":{"inform":{"instruction":"Say it.","points":{"office":"Where it is."},"knowledge":[{"knowledgeBase":"faq","searchQueries":["hours"," "]}]}}}}]}
            """;

        Assert.Equal(MistakeCodes.ReferenceMissing, Assert.Single(DefinitionValidator.ValidateJson(missing, ValidationStage.Save)).Code);
        Assert.Equal(
            [MistakeCodes.KnowledgeQueryBlank, MistakeCodes.KnowledgeQueryNoArticles],
            DefinitionValidator.ValidateJson(unfinished, ValidationStage.Version).Select(mistake => mistake.Code)
        );
    }

    [Fact]
    [Trait("criterion", "AC-procedure-creator-34")]
    public void An_article_outside_its_entrys_base_is_refused_with_where_it_belongs()
    {
        var mistake = Assert.Single(Check(ValidationStage.Save, InformingWith(Entry(articles: ["officeContact"])), Office("offer")));

        Assert.Equal(MistakeCodes.KnowledgeArticleOtherBase, mistake.Code);
        Assert.Equal("steps.only.inform.knowledge[0].articles[0]", mistake.Path);
        Assert.Contains("'offer'", mistake.Message, StringComparison.Ordinal);
        Assert.Contains("Move it to an entry for 'offer'", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-creator-35")]
    public void One_base_twice_on_an_instruction_is_refused_saying_to_merge_them_and_two_instructions_may_share_it()
    {
        var mistake = Assert.Single(Check(ValidationStage.Save, InformingWith(Entry(queries: ["a"]), Entry(queries: ["b"]))));

        Assert.Equal(MistakeCodes.KnowledgeBaseRepeated, mistake.Code);
        Assert.Equal("steps.only.inform.knowledge[1]", mistake.Path);
        Assert.Contains("Merge", mistake.Fix, StringComparison.Ordinal);

        var only = InformingWith(Entry(queries: ["a"]));
        var both = only with
        {
            FirstStep = "first",
            Steps = new Dictionary<string, ProcedureStep>
            {
                ["first"] = InformingWith(Entry(queries: ["b"])).Steps["only"] with { Next = new NextSteps { Step = "only" } },
                ["only"] = only.Steps["only"],
            },
        };
        Assert.Empty(Check(ValidationStage.Save, both, Office()));
    }

    [Fact]
    [Trait("criterion", "AC-procedure-creator-40")]
    public void A_declaration_without_knowledge_refuses_each_instruction_holding_it_at_version_only()
    {
        var definition = Bot(InformingWith(Entry(articles: ["officeContact"])), Office());
        var declaration = new BotDeclaration
        {
            Bot = "b",
            FormatVersion = BotDefinition.CurrentFormatVersion,
            Blocks = Block.Names,
            EmbeddingProfiles = [KnowledgeBase.DefaultEmbedding],
        };

        var mistake = Assert.Single(DefinitionValidator.Validate(definition, ValidationStage.Version, declaration));

        Assert.Equal(MistakeCodes.DeclarationKnowledge, mistake.Code);
        Assert.Equal("steps.only.inform.knowledge", mistake.Path);
        Assert.Contains("Knowledge = true", mistake.Fix, StringComparison.Ordinal);
        Assert.Empty(DefinitionValidator.Validate(definition, ValidationStage.Save, declaration));
        Assert.Empty(DefinitionValidator.Validate(definition, ValidationStage.Version, declaration with { Knowledge = true }));
        Assert.Empty(DefinitionValidator.Validate(definition, ValidationStage.Version, (BotDeclaration?)null));
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-7")]
    public void A_query_names_nothing_so_the_walker_yields_the_base_and_the_articles()
    {
        var references = ReferenceWalker.From(InformingWith(Entry(articles: ["officeContact"], queries: ["pricing"]))).ToList();

        Assert.Equal(
            [
                (new ResourceRef(ResourceKind.KnowledgeBase, "faq"), "steps.only.inform.knowledge[0].knowledgeBase"),
                (new ResourceRef(ResourceKind.Article, "officeContact"), "steps.only.inform.knowledge[0].articles[0]"),
            ],
            references.Select(reference => (reference.To, reference.Path))
        );
    }
}
