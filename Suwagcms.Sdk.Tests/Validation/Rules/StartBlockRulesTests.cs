using System.Text.Json;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.Procedures.Blocks;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;
using Suwagcms.Sdk.Validation.Rules;

namespace Suwagcms.Sdk.Tests.Validation.Rules;

public sealed class StartBlockRulesTests
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static Procedure Starting(string id, string target, string? next = null) =>
        Samples.Procedure(id) with
        {
            FirstStep = "get",
            Steps = new Dictionary<string, ProcedureStep>
            {
                ["get"] = new() { Start = target, Next = next is null ? null : new NextSteps { Step = next } },
                ["confirm"] = new() { Say = "Thank you." },
            },
        };

    private static Procedure Conversation => Samples.Procedure(Procedure.ConversationId);

    private static IReadOnlyList<Mistake> Check(ValidationStage stage, params Procedure[] procedures) =>
        DefinitionValidator.Validate(ProcedureSamples.Bot(procedures), stage);

    [Fact]
    [Trait("criterion", "AC-start-block-1")]
    public void A_start_step_keeps_its_target_and_next_through_the_format()
    {
        var procedure = Starting("orderValuation", "documentsList", "confirm");

        var back = JsonSerializer.Deserialize<Procedure>(JsonSerializer.Serialize(procedure, Wire), Wire);

        Assert.NotNull(back);
        var block = Assert.IsType<StartBlock>(Assert.Single(back.Steps["get"].Blocks));
        Assert.Equal("documentsList", block.Target);
        Assert.Equal("confirm", back.Steps["get"].Next?.Step);
    }

    [Fact]
    [Trait("criterion", "AC-start-block-2")]
    public void A_target_the_bot_does_not_have_is_refused_naming_the_step_and_listing_the_procedures()
    {
        var mistake = Assert.Single(
            Check(
                ValidationStage.Save,
                Conversation,
                Samples.Procedure("documentsList"),
                Starting("orderValuation", "documentList", "confirm")
            )
        );

        Assert.Equal(MistakeCodes.ReferenceMissing, mistake.Code);
        Assert.Equal(new ResourceRef(ResourceKind.Procedure, "orderValuation"), mistake.Resource);
        Assert.Equal("steps.get.start", mistake.Path);
        Assert.Equal(
            "'start' of step 'get' in 'orderValuation' names procedure 'documentList', which this bot does not have.",
            mistake.Message
        );
        Assert.Equal("Procedures: conversation, documentsList, orderValuation.", mistake.Fix);
    }

    [Fact]
    [Trait("criterion", "AC-start-block-3")]
    public void The_target_conversation_is_refused_with_a_fix_and_no_other_mistake()
    {
        var mistake = Assert.Single(Check(ValidationStage.Save, Starting("orderValuation", Procedure.ConversationId)));

        Assert.Equal(MistakeCodes.StartTargetConversation, mistake.Code);
        Assert.Equal("steps.get.start", mistake.Path);
        Assert.Contains("base of every conversation", mistake.Message, StringComparison.Ordinal);
        Assert.Contains("leaving out 'next'", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-start-block-3")]
    public void A_step_of_conversation_starting_conversation_is_refused_with_the_same_code_and_not_as_a_loop()
    {
        var mistake = Assert.Single(Check(ValidationStage.Save, Starting(Procedure.ConversationId, Procedure.ConversationId)));

        Assert.Equal(MistakeCodes.StartTargetConversation, mistake.Code);
    }

    [Theory]
    [Trait("criterion", "AC-start-block-4")]
    [InlineData("Documents List", "documentsList")]
    [InlineData("documents_list", "documentsList")]
    public void A_target_that_is_not_camelCase_is_refused_with_the_id_to_use(string target, string suggested)
    {
        var mistake = Assert.Single(Check(ValidationStage.Save, Conversation, Starting("orderValuation", target)));

        Assert.Equal(MistakeCodes.ReferenceMalformed, mistake.Code);
        Assert.Equal("steps.get.start", mistake.Path);
        Assert.Contains($"'{suggested}'", mistake.Fix, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("criterion", "AC-start-block-5")]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_target_passes_the_save_stage_and_is_a_version_mistake(string target)
    {
        Assert.Empty(Check(ValidationStage.Save, Conversation, Starting("orderValuation", target)));

        var mistake = Assert.Single(Check(ValidationStage.Version, Conversation, Starting("orderValuation", target, "confirm")));

        Assert.Equal(MistakeCodes.StepBlockEmpty, mistake.Code);
        Assert.Equal(ValidationStage.Version, mistake.Stage);
        Assert.Equal("steps.get.start", mistake.Path);
    }

    [Fact]
    [Trait("criterion", "AC-start-block-6")]
    public void A_start_beside_another_block_is_refused_and_the_lists_of_blocks_name_start()
    {
        var two = ProcedureSamples.Alone(new ProcedureStep { Start = "documentsList", Say = "Hi" });

        var multiple = Assert.Single(Check(ValidationStage.Save, Conversation, Samples.Procedure("documentsList"), two));

        Assert.Equal(MistakeCodes.StepBlockMultiple, multiple.Code);
        Assert.Contains("say, start", multiple.Message, StringComparison.Ordinal);
        Assert.Contains("start", multiple.Fix, StringComparison.Ordinal);
        Assert.Contains("start", Block.Names);
    }

    [Fact]
    [Trait("criterion", "AC-start-block-7")]
    public void A_step_of_conversation_may_start_a_procedure_and_two_procedures_may_start_one_target_with_mistakes_of_its_own()
    {
        var unfinished = new Procedure { Id = "documentsList", Description = "Unfinished, with no first step." };

        Assert.Empty(
            Check(
                ValidationStage.Save,
                Starting(Procedure.ConversationId, "documentsList", "confirm"),
                Starting("orderValuation", "documentsList", "confirm"),
                unfinished
            )
        );
    }

    [Fact]
    [Trait("criterion", "AC-start-block-8")]
    public void The_validator_on_its_own_reports_each_mistake_with_the_same_code_from_json()
    {
        const string json = """
            {"formatVersion":2,"procedures":[
              {"id":"conversation","description":"d","firstStep":"a","steps":{"a":{"say":"Hi"}}},
              {"id":"p","description":"d","firstStep":"a","steps":{
                "a":{"start":"ghost","next":"b"},
                "b":{"start":"conversation","next":"c"},
                "c":{"start":"Bad Id","next":"d"},
                "d":{"start":" ","say":"two"},
                "e":{"start":" "}}}]}
            """;

        var save = DefinitionValidator.ValidateJson(json, ValidationStage.Save);
        var version = DefinitionValidator.ValidateJson(json, ValidationStage.Version);

        Assert.Equal(
            [
                (MistakeCodes.ReferenceMissing, "steps.a.start"),
                (MistakeCodes.ReferenceMalformed, "steps.c.start"),
                (MistakeCodes.StepBlockMultiple, "steps.d"),
                (MistakeCodes.StartTargetConversation, "steps.b.start"),
            ],
            save.Select(mistake => (mistake.Code, mistake.Path))
        );
        Assert.Contains(version, mistake => (mistake.Code, mistake.Path) == (MistakeCodes.StepBlockEmpty, "steps.e.start"));
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-7")]
    public void A_null_in_the_lists_a_step_targets_is_not_a_crash_and_the_walker_yields_the_start_with_no_fact_use()
    {
        var reference = Assert.Single(ReferenceWalker.From(Starting("orderValuation", "documentsList", "confirm")));

        Assert.Equal(new ResourceRef(ResourceKind.Procedure, "documentsList"), reference.To);
        Assert.Equal("steps.get.start", reference.Path);
        Assert.Null(reference.Use);
        Assert.False(ResourceIds.IsCamelCase(null));
    }
}
