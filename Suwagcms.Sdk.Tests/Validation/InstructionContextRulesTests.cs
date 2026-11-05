using System.Text.Json;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation;

public sealed class InstructionContextRulesTests
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly KnowledgeBase Offer = new() { Id = "offer", Title = "Offer" };

    private static Article Office(string knowledgeBase = "faq") =>
        Samples.Pricing with
        {
            Id = "officeContact",
            KnowledgeBase = knowledgeBase,
        };

    private static Procedure Stalling(InstructionContext? context) =>
        Samples.Procedure("orderValuation") with
        {
            WhenStalled = new StallInstruction { Instruction = "Say how to reach the office.", Context = context },
        };

    private static Procedure InformingWith(InstructionContext context) =>
        ProcedureSamples.Alone(
            new ProcedureStep
            {
                Inform = new InformArguments
                {
                    Instruction = "Tell it.",
                    Points = new Dictionary<string, string?> { ["office"] = "Where the office is." },
                    Context = context,
                },
            }
        );

    private static BotDefinition Bot(Procedure procedure, params Article[] articles) =>
        new()
        {
            FormatVersion = 1,
            Facts = ProcedureSamples.Facts,
            KnowledgeBases = [Samples.Faq, Offer],
            Articles = articles,
            Procedures = [Samples.Procedure(Procedure.ConversationId), procedure],
        };

    private static IReadOnlyList<Mistake> Check(ValidationStage stage, Procedure procedure, params Article[] articles) =>
        DefinitionValidator.Validate(Bot(procedure, articles), stage);

    [Fact]
    [Trait("criterion", "AC-instruction-context-1")]
    public void A_context_with_articles_and_queries_reads_back_as_written_through_the_format()
    {
        var procedure = Stalling(new InstructionContext { Articles = ["officeContact"], SearchQueries = ["how long a valuation takes"] });

        var back = JsonSerializer.Deserialize<Procedure>(JsonSerializer.Serialize(procedure, Wire), Wire);

        Assert.NotNull(back?.WhenStalled?.Context);
        Assert.Equal(["officeContact"], back.WhenStalled.Context.Articles);
        Assert.Equal(["how long a valuation takes"], back.WhenStalled.Context.SearchQueries);
    }

    public static TheoryData<InstructionContext?> Harmless =>
        new(
            null,
            new InstructionContext(),
            new InstructionContext { Articles = [], SearchQueries = [] },
            new InstructionContext { Articles = ["officeContact"] },
            new InstructionContext { SearchQueries = ["opening hours"] }
        );

    [Theory]
    [Trait("criterion", "AC-instruction-context-2")]
    [MemberData(nameof(Harmless))]
    public void No_context_an_empty_one_empty_lists_and_one_key_alone_are_each_accepted_at_every_stage(InstructionContext? context) =>
        Assert.Empty(Check(ValidationStage.Version, Stalling(context), Office()));

    [Fact]
    [Trait("criterion", "AC-instruction-context-3")]
    public void An_article_the_bot_does_not_have_is_refused_naming_where_the_context_is_and_one_in_another_base_is_accepted()
    {
        var mistake = Assert.Single(
            Check(ValidationStage.Save, Stalling(new InstructionContext { Articles = ["officeContacts"] }), Office())
        );

        Assert.Equal(MistakeCodes.ReferenceMissing, mistake.Code);
        Assert.Equal("whenStalled.context.articles[0]", mistake.Path);
        Assert.Equal(
            "'context' of whenStalled in 'orderValuation' names article 'officeContacts', which this bot does not have.",
            mistake.Message
        );
        Assert.Contains("'suwagcms articles list' shows them", mistake.Fix, StringComparison.Ordinal);
        Assert.Empty(Check(ValidationStage.Save, Stalling(new InstructionContext { Articles = ["officeContact"] }), Office("offer")));
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-4")]
    public void An_article_id_that_is_not_camelCase_is_refused_with_the_id_to_use_and_not_also_as_missing()
    {
        var mistake = Assert.Single(Check(ValidationStage.Save, Stalling(new InstructionContext { Articles = ["Office Contact"] })));

        Assert.Equal(MistakeCodes.ReferenceMalformed, mistake.Code);
        Assert.Contains("'officeContact'", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-4")]
    public void A_context_on_a_block_with_no_instruction_is_refused_saying_where_it_belongs()
    {
        const string json = """
            {"formatVersion":1,"procedures":[{"id":"p","description":"d","firstStep":"a","steps":{
              "a":{"say":"Hi","context":{"articles":["officeContact"]}}}}]}
            """;

        var mistake = Assert.Single(DefinitionValidator.ValidateJson(json, ValidationStage.Save));

        Assert.Equal(MistakeCodes.StepBlockUnknown, mistake.Code);
        Assert.Equal("steps.a.context", mistake.Path);
        Assert.Contains("whenStalled", mistake.Fix, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("criterion", "AC-instruction-context-5")]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_query_passes_the_save_stage_and_is_a_version_mistake(string query)
    {
        var procedure = Stalling(new InstructionContext { SearchQueries = ["opening hours", query] });

        Assert.Empty(Check(ValidationStage.Save, procedure, Office()));

        var mistake = Assert.Single(Check(ValidationStage.Version, procedure, Office()));
        Assert.Equal(MistakeCodes.ContextQueryBlank, mistake.Code);
        Assert.Equal("whenStalled.context.searchQueries[1]", mistake.Path);
        Assert.Equal("'context' of whenStalled in 'orderValuation' has a blank search query.", mistake.Message);
        Assert.Equal("Write the query, or remove it.", mistake.Fix);
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-6")]
    public void A_query_in_a_bot_with_no_articles_is_a_version_mistake_that_goes_once_the_bot_has_an_article()
    {
        var procedure = Stalling(new InstructionContext { SearchQueries = ["opening hours"] });

        Assert.Empty(Check(ValidationStage.Save, procedure));
        Assert.Equal(MistakeCodes.ContextQueryNoArticles, Assert.Single(Check(ValidationStage.Version, procedure)).Code);
        Assert.Empty(Check(ValidationStage.Version, procedure, Office()));
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-8")]
    public void The_validator_on_its_own_reports_the_reference_and_version_mistakes_with_the_same_codes_from_json()
    {
        const string missing = """
            {"formatVersion":1,"procedures":[{"id":"p","description":"d","firstStep":"a","steps":{"a":{"say":"Hi"}},
              "whenStalled":{"instruction":"Say it.","context":{"articles":["ghost"]}}}]}
            """;
        const string unfinished = """
            {"formatVersion":1,"procedures":[{"id":"conversation","description":"d","firstStep":"a","steps":{"a":{"say":"Hi"}},
              "whenStalled":{"instruction":"Say it.","context":{"searchQueries":["hours"," "]}}}]}
            """;

        Assert.Equal(MistakeCodes.ReferenceMissing, Assert.Single(DefinitionValidator.ValidateJson(missing, ValidationStage.Save)).Code);
        Assert.Equal(
            [MistakeCodes.ContextQueryBlank, MistakeCodes.ContextQueryNoArticles],
            DefinitionValidator.ValidateJson(unfinished, ValidationStage.Version).Select(mistake => mistake.Code)
        );
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-10")]
    public void The_instruction_of_an_inform_takes_a_context_under_the_same_rules_as_on_when_stalled()
    {
        var missing = Assert.Single(Check(ValidationStage.Save, InformingWith(new InstructionContext { Articles = ["ghost"] })));
        var blank = Check(ValidationStage.Version, InformingWith(new InstructionContext { SearchQueries = [" ", "hours"] }));

        Assert.Equal((MistakeCodes.ReferenceMissing, "steps.only.inform.context.articles[0]"), (missing.Code, missing.Path));
        Assert.Equal("'context' of step 'only' in 'p' names article 'ghost', which this bot does not have.", missing.Message);
        Assert.Equal(
            [
                (MistakeCodes.ContextQueryBlank, "steps.only.inform.context.searchQueries[0]"),
                (MistakeCodes.ContextQueryNoArticles, "steps.only.inform.context.searchQueries"),
            ],
            blank.Select(mistake => (mistake.Code, mistake.Path))
        );
        Assert.Empty(
            Check(
                ValidationStage.Version,
                InformingWith(new InstructionContext { Articles = ["officeContact"], SearchQueries = ["hours"] }),
                Office()
            )
        );
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-7")]
    public void A_query_names_nothing_so_the_walker_yields_only_the_articles()
    {
        var reference = Assert.Single(
            ReferenceWalker.From(Stalling(new InstructionContext { Articles = ["officeContact"], SearchQueries = ["pricing"] }))
        );

        Assert.Equal(new ResourceRef(ResourceKind.Article, "officeContact"), reference.To);
        Assert.Equal("whenStalled.context.articles[0]", reference.Path);
    }
}
