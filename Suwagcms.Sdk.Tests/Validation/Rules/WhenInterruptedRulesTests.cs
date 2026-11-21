using System.Text.Json;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation.Rules;

public sealed class WhenInterruptedRulesTests
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    private static IReadOnlyList<Mistake> Check(Procedure procedure) =>
        DefinitionValidator.Validate(ProcedureSamples.Bot(procedure), ValidationStage.Save);

    [Theory]
    [Trait("criterion", "AC-chatbot-contract-1")]
    [InlineData("keep", false)]
    [InlineData("discard", true)]
    public void A_procedure_holds_the_choice_and_reads_it_back(string value, bool discarded)
    {
        var procedure = ProcedureSamples.All with { WhenInterrupted = value };

        var back = JsonSerializer.Deserialize<Procedure>(JsonSerializer.Serialize(procedure, Wire), Wire);

        Assert.Empty(Check(procedure));
        Assert.Equal(value, back?.WhenInterrupted);
        Assert.Equal(discarded, back?.IsDiscardedWhenInterrupted);
    }

    [Fact]
    [Trait("criterion", "AC-chatbot-contract-1")]
    public void A_procedure_without_the_choice_has_no_such_member_and_counts_as_keep()
    {
        var json = JsonSerializer.Serialize(ProcedureSamples.All, Wire);

        Assert.DoesNotContain("whenInterrupted", json, StringComparison.Ordinal);
        Assert.False(ProcedureSamples.All.IsDiscardedWhenInterrupted);
        Assert.Empty(Check(ProcedureSamples.All));
    }

    [Theory]
    [Trait("criterion", "AC-chatbot-contract-2")]
    [InlineData("sometimes")]
    [InlineData("Discard")]
    [InlineData("")]
    public void Any_other_value_is_refused_with_the_allowed_values(string value)
    {
        var mistake = Assert.Single(Check(ProcedureSamples.All with { WhenInterrupted = value }));

        Assert.Equal(MistakeCode.ProcedureWhenInterruptedInvalid, mistake.Code);
        Assert.Equal(ValidationStage.Save, mistake.Stage);
        Assert.Equal("whenInterrupted", mistake.Path);
        Assert.Contains("keep, discard", mistake.Fix, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("criterion", "AC-chatbot-contract-3")]
    [InlineData("keep")]
    [InlineData("discard")]
    public void The_field_on_conversation_is_refused_with_a_fix_saying_to_remove_it(string value)
    {
        var conversation = Samples.Procedure(Procedure.ConversationId) with { WhenInterrupted = value };

        var mistake = Assert.Single(Check(conversation));

        Assert.Equal(MistakeCode.ProcedureWhenInterruptedOnConversation, mistake.Code);
        Assert.Contains("Remove whenInterrupted", mistake.Fix, StringComparison.Ordinal);
    }
}
