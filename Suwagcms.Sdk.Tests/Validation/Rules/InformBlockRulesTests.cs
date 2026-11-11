using System.Text.Json;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Articles;
using Suwagcms.Sdk.Format.KnowledgeBases;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.Procedures.Blocks;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation.Rules;

public sealed class InformBlockRulesTests
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

    private static readonly Dictionary<string, string?> TwoPoints = new()
    {
        ["turnaround"] = "The report is ready within 7 working days.",
        ["delivery"] = "The report is sent by e-mail.",
    };

    private static Article Office(string id = "officeContact", string knowledgeBase = "faq") =>
        Samples.Pricing with
        {
            Id = id,
            KnowledgeBase = knowledgeBase,
        };

    private static Procedure Informing(
        IReadOnlyDictionary<string, string?>? points = null,
        string instruction = "Tell the customer.",
        IReadOnlyList<string>? articles = null
    ) =>
        ProcedureSamples.Alone(
            new ProcedureStep
            {
                Inform = new InformArguments
                {
                    Instruction = instruction,
                    Points = points,
                    Knowledge = articles is null ? null : [new KnowledgeEntry { KnowledgeBase = "faq", Articles = articles }],
                },
            }
        );

    private static BotDefinition Bot(Procedure procedure, params Article[] articles) =>
        new()
        {
            FormatVersion = 5,
            Facts = ProcedureSamples.Facts,
            KnowledgeBases = [Samples.Faq, Offer],
            Articles = articles,
            Procedures = [Samples.Procedure(Procedure.ConversationId), procedure],
        };

    private static IReadOnlyList<Mistake> Save(Procedure procedure, params Article[] articles) =>
        DefinitionValidator.Validate(Bot(procedure, articles), ValidationStage.Save);

    [Fact]
    [Trait("criterion", "AC-inform-block-1")]
    public void An_inform_keeps_its_instruction_its_knowledge_and_its_points_in_the_order_written_through_the_format()
    {
        var procedure = Informing(TwoPoints, articles: ["zebra", "alpha"]);

        var back = JsonSerializer.Deserialize<Procedure>(JsonSerializer.Serialize(procedure, Wire), Wire);

        Assert.NotNull(back);
        var block = Assert.IsType<InformBlock>(Assert.Single(back.Steps["only"].Blocks));
        Assert.Equal("Tell the customer.", block.Arguments.Instruction);
        Assert.Equal(["zebra", "alpha"], Assert.Single(block.Arguments.Knowledge ?? []).Articles);
        Assert.Equal(["turnaround", "delivery"], block.Arguments.Points?.Keys);
    }

    [Fact]
    [Trait("criterion", "AC-inform-block-2")]
    public void An_article_the_bot_does_not_have_is_refused_naming_the_step_and_one_in_another_base_is_refused_too()
    {
        var mistake = Assert.Single(Save(Informing(TwoPoints, articles: ["officeContacts"]), Office()));

        Assert.Equal(MistakeCodes.ReferenceMissing, mistake.Code);
        Assert.Equal("steps.only.inform.knowledge[0].articles[0]", mistake.Path);
        Assert.Contains("'knowledge' of step 'only'", mistake.Message, StringComparison.Ordinal);

        Assert.Equal(
            MistakeCodes.KnowledgeArticleOtherBase,
            Assert.Single(Save(Informing(TwoPoints, articles: ["officeContact"]), Office(knowledgeBase: "offer"))).Code
        );
    }

    [Fact]
    [Trait("criterion", "AC-inform-block-5")]
    public void A_save_holding_several_bad_names_returns_a_mistake_for_each_at_once()
    {
        var points = new Dictionary<string, string?> { ["bad_name"] = "Text.", ["Other Name"] = "Text." };

        Assert.Equal(
            [
                "steps.only.inform.knowledge[0].articles[0]",
                "steps.only.inform.knowledge[0].articles[1]",
                "steps.only.inform.points.Other Name",
                "steps.only.inform.points.bad_name",
            ],
            Save(Informing(points, articles: ["one", "two"])).Select(mistake => mistake.Path).Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    [Trait("criterion", "AC-inform-block-5")]
    public void An_id_in_the_wrong_form_or_null_is_refused_as_malformed_and_not_also_as_missing()
    {
        const string json = """
            {"formatVersion":5,"knowledgeBases":[{"id":"faq","title":"F","embedding":"default","results":5}],"procedures":[{"id":"p","description":"d","firstStep":"a","steps":{
              "a":{"inform":{"instruction":"Say it.","points":{"one":"Text."},"knowledge":[{"knowledgeBase":"faq","articles":["Office Contact",null]}]}}}}]}
            """;

        var mistakes = DefinitionValidator.ValidateJson(json, ValidationStage.Save);

        Assert.All(mistakes, mistake => Assert.Equal(MistakeCodes.ReferenceMalformed, mistake.Code));
        Assert.Equal(2, mistakes.Count);
        Assert.Contains(mistakes, mistake => mistake.Fix.Contains("'officeContact'", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("criterion", "AC-inform-block-6")]
    public void No_points_a_blank_point_and_a_blank_instruction_pass_the_save_stage_and_are_version_mistakes()
    {
        var nothing = Informing(instruction: " ");
        var blank = Informing(new Dictionary<string, string?> { ["turnaround"] = " ", ["delivery"] = null });

        Assert.Empty(Save(nothing));
        Assert.Empty(Save(blank));
        Assert.Equal(
            [(MistakeCodes.StepInformEmpty, "steps.only.inform"), (MistakeCodes.InstructionBlank, "steps.only.inform.instruction")],
            DefinitionValidator.Validate(Bot(nothing), ValidationStage.Version).Select(mistake => (mistake.Code, mistake.Path))
        );
        Assert.Equal(
            [
                (MistakeCodes.StepInformPointBlank, "steps.only.inform.points.delivery"),
                (MistakeCodes.StepInformPointBlank, "steps.only.inform.points.turnaround"),
            ],
            DefinitionValidator.Validate(Bot(blank), ValidationStage.Version).Select(mistake => (mistake.Code, mistake.Path))
        );
        Assert.Empty(DefinitionValidator.Validate(Bot(Informing(TwoPoints)), ValidationStage.Version));
    }

    [Fact]
    [Trait("criterion", "AC-inform-block-7")]
    public void An_inform_beside_another_block_is_refused_and_the_lists_of_blocks_name_inform()
    {
        var two = ProcedureSamples.Alone(
            new ProcedureStep
            {
                Inform = new InformArguments { Points = TwoPoints },
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
            {"formatVersion":5,"knowledgeBases":[{"id":"faq","title":"F","embedding":"default","results":5}],"procedures":[{"id":"p","description":"d","firstStep":"a","steps":{
              "a":{"inform":{"instruction":"Say it.","points":{"bad_name":"Text."},"knowledge":[{"knowledgeBase":"faq","articles":["ghost"]}]}}}}]}
            """;
        const string unfinished = """
            {"formatVersion":5,"procedures":[{"id":"conversation","description":"d","firstStep":"a","steps":{
              "a":{"inform":{}}}}]}
            """;

        Assert.Equal(
            [MistakeCodes.ReferenceMissing, MistakeCodes.StepInformPointName],
            DefinitionValidator.ValidateJson(missing, ValidationStage.Save).Select(mistake => mistake.Code)
        );
        Assert.Equal(
            [MistakeCodes.StepInformEmpty, MistakeCodes.InstructionBlank],
            DefinitionValidator.ValidateJson(unfinished, ValidationStage.Version).Select(mistake => mistake.Code)
        );
    }

    [Fact]
    [Trait("criterion", "AC-inform-block-11")]
    public void A_point_name_that_is_not_camel_case_is_refused_with_the_name_to_use()
    {
        var mistake = Assert.Single(Save(Informing(new Dictionary<string, string?> { ["when_ready"] = "Text." })));

        Assert.Equal(MistakeCodes.StepInformPointName, mistake.Code);
        Assert.Equal("steps.only.inform.points.when_ready", mistake.Path);
        Assert.Contains("'whenReady'", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-7")]
    public void The_walker_yields_the_base_and_the_articles_of_the_knowledge_with_their_place()
    {
        var references = ReferenceWalker.From(Informing(TwoPoints, articles: ["officeContact"])).ToList();

        Assert.Equal(
            [
                ("steps.only.inform.knowledge[0].knowledgeBase", ResourceKind.KnowledgeBase, "faq"),
                ("steps.only.inform.knowledge[0].articles[0]", ResourceKind.Article, "officeContact"),
            ],
            references.Select(reference => (reference.Path, reference.To.Kind, reference.To.Id))
        );
    }
}
