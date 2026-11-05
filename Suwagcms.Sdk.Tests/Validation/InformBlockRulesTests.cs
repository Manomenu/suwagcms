using System.Text.Json;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation;

public sealed class InformBlockRulesTests
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly KnowledgeBase Offer = new() { Id = "offer", Title = "Offer" };

    private static Article Office(string id = "officeContact", string knowledgeBase = "faq") =>
        Samples.Pricing with
        {
            Id = id,
            KnowledgeBase = knowledgeBase,
        };

    private static Procedure Informing(
        IReadOnlyList<string>? articles = null,
        IReadOnlyList<string>? facts = null,
        string instruction = "Tell the customer."
    ) =>
        ProcedureSamples.Alone(
            new ProcedureStep
            {
                Inform = new InformArguments
                {
                    Instruction = instruction,
                    Articles = articles,
                    Facts = facts,
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

    private static IReadOnlyList<Mistake> Save(Procedure procedure, params Article[] articles) =>
        DefinitionValidator.Validate(Bot(procedure, articles), ValidationStage.Save);

    [Fact]
    [Trait("criterion", "AC-inform-block-1")]
    public void An_inform_keeps_its_instruction_and_its_lists_in_the_order_written_through_the_format()
    {
        var procedure = Informing(["zebra", "alpha", "zebra"], ["order.kind", "customer.city"]);

        var back = JsonSerializer.Deserialize<Procedure>(JsonSerializer.Serialize(procedure, Wire), Wire);

        Assert.NotNull(back);
        var block = Assert.IsType<InformBlock>(Assert.Single(back.Steps["only"].Blocks));
        Assert.Equal("Tell the customer.", block.Arguments.Instruction);
        Assert.Equal(["zebra", "alpha", "zebra"], block.Arguments.Articles);
        Assert.Equal(["order.kind", "customer.city"], block.Arguments.Facts);
    }

    [Fact]
    [Trait("criterion", "AC-inform-block-2")]
    public void An_article_the_bot_does_not_have_is_refused_naming_the_step_and_one_in_another_base_is_accepted()
    {
        var mistake = Assert.Single(Save(Informing(["officeContacts"]), Office()));

        Assert.Equal(MistakeCodes.ReferenceMissing, mistake.Code);
        Assert.Equal(new ResourceRef(ResourceKind.Procedure, "p"), mistake.Resource);
        Assert.Equal("steps.only.inform.articles[0]", mistake.Path);
        Assert.Equal("'inform' of step 'only' in 'p' names article 'officeContacts', which this bot does not have.", mistake.Message);
        Assert.Contains("'suwagcms articles list' shows them", mistake.Fix, StringComparison.Ordinal);
        Assert.Empty(Save(Informing(["officeContact"]), Office("officeContact", "offer")));
    }

    [Fact]
    [Trait("criterion", "AC-inform-block-3")]
    public void A_fact_the_bot_does_not_have_is_refused_naming_the_step_and_listing_the_facts()
    {
        var mistake = Assert.Single(Save(Informing(facts: ["customer.cty"])));

        Assert.Equal(MistakeCodes.ReferenceMissing, mistake.Code);
        Assert.Equal("steps.only.inform.facts[0]", mistake.Path);
        Assert.Equal("'inform' of step 'only' in 'p' names fact 'customer.cty', which this bot does not have.", mistake.Message);
        Assert.Equal("Facts: customer.city, order.floors, order.kind, system.flag.", mistake.Fix);
    }

    [Theory]
    [Trait("criterion", "AC-inform-block-4")]
    [InlineData(FactWriters.Assign)]
    [InlineData(FactWriters.Set)]
    [InlineData(FactWriters.Correct)]
    public void A_fact_with_any_writable_by_can_be_named(string writableBy)
    {
        var fact = new GlobalFact
        {
            Name = "result.value",
            Type = FactTypes.Text,
            Description = "d",
            MaxLength = 9,
            WritableBy = writableBy,
        };

        Assert.Empty(
            DefinitionValidator.Validate(Bot(Informing(facts: ["result.value"])) with { Facts = [fact] }, ValidationStage.Version)
        );
    }

    [Fact]
    [Trait("criterion", "AC-inform-block-5")]
    public void A_save_holding_several_bad_names_returns_a_mistake_for_each_at_once() =>
        Assert.Equal(
            ["steps.only.inform.articles[0]", "steps.only.inform.articles[1]", "steps.only.inform.facts[0]"],
            Save(Informing(["one", "two"], ["three.four"])).Select(mistake => mistake.Path).Order(StringComparer.Ordinal)
        );

    [Fact]
    [Trait("criterion", "AC-inform-block-5")]
    public void An_id_or_fact_name_in_the_wrong_form_or_null_is_refused_as_malformed_and_not_also_as_missing()
    {
        const string json = """
            {"formatVersion":1,"procedures":[{"id":"p","description":"d","firstStep":"a","steps":{
              "a":{"inform":{"instruction":"Say it.","articles":["Office Contact",null],"facts":["Customer City"]}}}}]}
            """;

        var mistakes = DefinitionValidator.ValidateJson(json, ValidationStage.Save);

        Assert.All(mistakes, mistake => Assert.Equal(MistakeCodes.ReferenceMalformed, mistake.Code));
        Assert.Equal(3, mistakes.Count);
        Assert.Contains(mistakes, mistake => mistake.Fix.Contains("'officeContact'", StringComparison.Ordinal));
        Assert.Contains(mistakes, mistake => mistake.Fix.Contains("'customerCity'", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("criterion", "AC-inform-block-6")]
    public void An_empty_inform_and_a_blank_instruction_pass_the_save_stage_and_are_version_mistakes()
    {
        var nothing = Informing(instruction: " ");

        Assert.Empty(Save(nothing));
        Assert.Equal(
            [(MistakeCodes.StepInformEmpty, "steps.only.inform"), (MistakeCodes.InstructionBlank, "steps.only.inform.instruction")],
            DefinitionValidator.Validate(Bot(nothing), ValidationStage.Version).Select(mistake => (mistake.Code, mistake.Path))
        );
        Assert.Empty(DefinitionValidator.Validate(Bot(Informing(facts: ["order.kind"])), ValidationStage.Version));
    }

    [Fact]
    [Trait("criterion", "AC-inform-block-7")]
    public void An_inform_beside_another_block_is_refused_and_the_lists_of_blocks_name_inform()
    {
        var two = ProcedureSamples.Alone(
            new ProcedureStep
            {
                Inform = new InformArguments { Facts = ["order.kind"] },
                Say = "Hi",
            }
        );

        var mistake = Assert.Single(Save(two));

        Assert.Equal(MistakeCodes.StepBlockMultiple, mistake.Code);
        Assert.Contains("say, inform", mistake.Message, StringComparison.Ordinal);
        Assert.Contains("inform", mistake.Fix, StringComparison.Ordinal);
        Assert.Contains("inform", Block.Names);
    }

    [Fact]
    [Trait("criterion", "AC-inform-block-8")]
    public void The_validator_on_its_own_reports_the_reference_and_version_mistakes_with_the_same_codes_from_json()
    {
        const string missing = """
            {"formatVersion":1,"procedures":[{"id":"p","description":"d","firstStep":"a","steps":{
              "a":{"inform":{"instruction":"Say it.","articles":["ghost"],"facts":["no.fact"]}}}}]}
            """;
        const string unfinished = """
            {"formatVersion":1,"procedures":[{"id":"conversation","description":"d","firstStep":"a","steps":{
              "a":{"inform":{}}}}]}
            """;

        Assert.Equal(
            [MistakeCodes.ReferenceMissing, MistakeCodes.ReferenceMissing],
            DefinitionValidator.ValidateJson(missing, ValidationStage.Save).Select(mistake => mistake.Code)
        );
        Assert.Equal(
            [MistakeCodes.StepInformEmpty, MistakeCodes.InstructionBlank],
            DefinitionValidator.ValidateJson(unfinished, ValidationStage.Version).Select(mistake => mistake.Code)
        );
    }

    [Fact]
    public void The_walker_yields_each_named_article_and_fact_with_its_place_and_use()
    {
        var references = ReferenceWalker.From(Informing(["officeContact"], ["customer.city"])).ToList();

        Assert.Equal(
            [
                ("steps.only.inform.facts[0]", ResourceKind.GlobalFact, "customer.city", (FactUse?)FactUse.Inform),
                ("steps.only.inform.articles[0]", ResourceKind.Article, "officeContact", null),
            ],
            references.Select(reference => (reference.Path, reference.To.Kind, reference.To.Id, reference.Use))
        );
    }
}
