using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Actions;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation.Rules;

public sealed class ActionBlockRulesTests
{
    private static readonly ActionField Yes = new()
    {
        Type = FactTypes.Enum,
        Description = "d",
        Values = ["yes", "no"],
    };

    private static readonly ActionField Number = new()
    {
        Type = FactTypes.WholeNumber,
        Description = "d",
        Min = 1,
        Max = 100,
    };

    private static readonly ActionField Text = new()
    {
        Type = FactTypes.Text,
        Description = "d",
        MaxLength = 200,
    };

    private static BotAction Action(ActionField parameter, ActionField result, bool code = false) =>
        new()
        {
            Id = "check",
            Description = "Checks",
            Parameters = new Dictionary<string, ActionField> { ["input"] = parameter },
            Results = new Dictionary<string, ActionField> { ["output"] = result },
            Implementation = code
                ? new ActionImplementation { IsCode = true }
                : new ActionImplementation
                {
                    Http = new HttpCall { Method = "POST", Address = "https://x.internal" },
                },
        };

    private static GlobalFact Fact(ActionField field, string name = "the.fact", string writableBy = FactWriters.Set) =>
        field.Named(name) with
        {
            WritableBy = writableBy,
        };

    private static ProcedureStep Step(string? parameterFact = "the.fact", string? resultFact = null, string? onFailure = "failed") =>
        new()
        {
            Action = "check",
            With = parameterFact is null ? null : new Dictionary<string, string> { ["input"] = parameterFact },
            Results = resultFact is null ? null : new Dictionary<string, string> { ["output"] = resultFact },
            OnFailure = onFailure,
        };

    private static Procedure Procedure(ProcedureStep step, Dictionary<string, LocalFact>? locals = null) =>
        new()
        {
            Id = "p",
            Description = "d",
            FirstStep = "run",
            Steps = new Dictionary<string, ProcedureStep>
            {
                ["run"] = step,
                ["failed"] = new() { Say = "Sorry" },
            },
            LocalFacts = locals,
        };

    private static BotDefinition Bot(BotAction action, GlobalFact fact, ProcedureStep step) =>
        new()
        {
            FormatVersion = 1,
            Facts = [fact],
            Actions = [action],
            Procedures = [Procedure(step)],
        };

    private static IReadOnlyList<Mistake> Save(BotDefinition definition) => DefinitionValidator.Validate(definition, ValidationStage.Save);

    private static List<Mistake> OfProcedures(BotDefinition definition, ValidationStage stage) =>
        [
            .. DefinitionValidator
                .Validate(definition, stage)
                .Where(mistake => mistake.Resource.Kind == ResourceKind.Procedure && mistake.Stage == stage),
        ];

    [Fact]
    [Trait("criterion", "AC-action-block-3")]
    public void An_action_the_bot_does_not_have_is_a_missing_reference_listing_the_ones_it_has()
    {
        var definition = Bot(Action(Text, Text), Fact(Text), new ProcedureStep { Action = "ghost", OnFailure = "failed" });

        var mistake = Assert.Single(Save(definition));

        Assert.Equal(MistakeCodes.ReferenceMissing, mistake.Code);
        Assert.Equal("steps.run.action", mistake.Path);
        Assert.Equal("Actions of the bot: check.", mistake.Fix);
    }

    [Fact]
    [Trait("criterion", "AC-action-block-4")]
    public void A_parameter_or_result_the_action_does_not_have_is_refused_with_the_ones_it_has()
    {
        var step = new ProcedureStep
        {
            Action = "check",
            With = new Dictionary<string, string> { ["nope"] = "the.fact" },
            Results = new Dictionary<string, string> { ["outptu"] = "the.fact" },
            OnFailure = "failed",
        };

        var mistakes = Save(Bot(Action(Text, Text), Fact(Text), step));

        Assert.Contains(
            mistakes,
            mistake =>
                mistake.Path == "steps.run.with.nope"
                && mistake.Fix == "Parameters: input."
                && mistake.Code == MistakeCodes.ReferenceMissing
        );
        Assert.Contains(
            mistakes,
            mistake =>
                mistake.Path == "steps.run.results.outptu"
                && mistake.Fix == "Results: output."
                && mistake.Code == MistakeCodes.ReferenceMissing
        );
    }

    [Fact]
    [Trait("criterion", "AC-action-block-5")]
    public void A_fact_the_bot_does_not_have_is_a_missing_reference_with_the_bots_facts()
    {
        var mistakes = Save(Bot(Action(Text, Text), Fact(Text), Step("ghost.fact", "other.ghost")));

        foreach (var path in new[] { "steps.run.with.input", "steps.run.results.output" })
        {
            var mistake = Assert.Single(mistakes, found => found.Path == path);
            Assert.Equal(MistakeCodes.ReferenceMissing, mistake.Code);
            Assert.Contains("the.fact", mistake.Fix, StringComparison.Ordinal);
        }
    }

    public static TheoryData<ActionField, ActionField, string?> ParameterFits =>
        new()
        {
            // Why: the fact is the source: all it can hold must fit the parameter.
            { Yes, Yes with { Values = ["yes", "no", "maybe"] }, null },
            { Yes with { Values = ["yes", "no", "maybe"] }, Yes, MistakeCodes.ActionMappingTooWide },
            { Number with { Min = 2, Max = 100 }, Number, null },
            { Number with { Min = 0, Max = 500 }, Number, MistakeCodes.ActionMappingTooWide },
            { Text with { MaxLength = 200 }, Text, null },
            { Text with { MaxLength = 500 }, Text, MistakeCodes.ActionMappingTooWide },
            { Number, Text, MistakeCodes.ActionMappingTypeMismatch },
            { Yes, Text, MistakeCodes.ActionMappingTypeMismatch },
        };

    [Theory]
    [MemberData(nameof(ParameterFits))]
    [Trait("criterion", "AC-action-block-6")]
    public void A_parameter_mapping_is_saved_when_all_the_fact_can_hold_fits_and_refused_naming_both_types_otherwise(
        ActionField fact,
        ActionField parameter,
        string? code
    )
    {
        ArgumentNullException.ThrowIfNull(fact);
        var mistakes = Save(Bot(Action(parameter, Text), Fact(fact), Step()));

        if (code is null)
        {
            Assert.Empty(mistakes);
            return;
        }
        var mistake = Assert.Single(mistakes);
        Assert.Equal(code, mistake.Code);
        Assert.Contains("fact 'the.fact' (", mistake.Message, StringComparison.Ordinal);
        Assert.Contains("parameter 'input' (", mistake.Message, StringComparison.Ordinal);
    }

    public static TheoryData<ActionField, ActionField, string?> ResultFits =>
        new()
        {
            // Why: the result is the source: all it can hold must fit the fact.
            { Yes, Yes with { Values = ["yes", "no", "maybe"] }, null },
            { Yes with { Values = ["yes", "no", "maybe"] }, Yes, MistakeCodes.ActionMappingTooWide },
            { Number with { Min = 2 }, Number, null },
            { Number with { Max = 500 }, Number, MistakeCodes.ActionMappingTooWide },
            { Text with { MaxLength = 10 }, Text, null },
            { Text with { MaxLength = 500 }, Text, MistakeCodes.ActionMappingTooWide },
            { Number, Text, MistakeCodes.ActionMappingTypeMismatch },
        };

    [Theory]
    [MemberData(nameof(ResultFits))]
    [Trait("criterion", "AC-action-block-7")]
    public void A_result_mapping_is_saved_when_all_the_result_can_hold_fits_and_refused_naming_both_types_otherwise(
        ActionField result,
        ActionField fact,
        string? code
    )
    {
        ArgumentNullException.ThrowIfNull(fact);
        var mistakes = Save(Bot(Action(Text, result), Fact(fact), Step(null, "the.fact")));

        if (code is null)
        {
            Assert.Empty(mistakes);
            return;
        }
        var mistake = Assert.Single(mistakes);
        Assert.Equal(code, mistake.Code);
        Assert.Contains("result 'output' (", mistake.Message, StringComparison.Ordinal);
        Assert.Contains("fact 'the.fact' (", mistake.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(FactWriters.Assign)]
    [InlineData(FactWriters.Set)]
    [InlineData(FactWriters.Correct)]
    [Trait("criterion", "AC-action-block-8")]
    public void A_result_may_be_mapped_to_a_fact_of_any_writable_by(string writableBy) =>
        Assert.Empty(Save(Bot(Action(Text, Text), Fact(Text, writableBy: writableBy), Step(null, "the.fact"))));

    [Fact]
    [Trait("criterion", "AC-action-block-9")]
    public void A_result_left_unmapped_is_no_mistake_at_any_stage()
    {
        var definition = Bot(Action(Text, Text), Fact(Text), Step());

        Assert.Empty(OfProcedures(definition, ValidationStage.Save));
        Assert.DoesNotContain(
            OfProcedures(definition, ValidationStage.Version),
            mistake => mistake.Path.Contains("results", StringComparison.Ordinal)
        );
    }

    [Fact]
    [Trait("criterion", "AC-action-block-9")]
    public void Two_results_mapped_to_one_fact_are_refused()
    {
        var action = Action(Text, Text) with
        {
            Results = new Dictionary<string, ActionField> { ["one"] = Text, ["two"] = Text },
        };
        var step = new ProcedureStep
        {
            Action = "check",
            Results = new Dictionary<string, string> { ["one"] = "the.fact", ["two"] = "the.fact" },
        };

        var mistake = Assert.Single(Save(Bot(action, Fact(Text), step)));

        Assert.Equal(MistakeCodes.ActionMappingResultTwice, mistake.Code);
    }

    [Fact]
    [Trait("criterion", "AC-action-block-10")]
    public void On_failure_naming_a_missing_step_is_refused_with_the_steps()
    {
        var mistake = Assert.Single(Save(Bot(Action(Text, Text), Fact(Text), Step(onFailure: "ghost"))));

        Assert.Equal(MistakeCodes.StepMissing, mistake.Code);
        Assert.Equal("steps.run.onFailure", mistake.Path);
        Assert.Contains("failed", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-action-block-10")]
    public void On_failure_on_a_step_with_another_block_is_refused()
    {
        var step = new ProcedureStep { Say = "Hi", OnFailure = "failed" };

        var mistake = Assert.Single(Save(Bot(Action(Text, Text), Fact(Text), step)));

        Assert.Equal(MistakeCodes.StepActionFieldForeign, mistake.Code);
        Assert.Equal("steps.run.onFailure", mistake.Path);
    }

    [Fact]
    [Trait("criterion", "AC-action-block-11")]
    public void The_version_stage_reports_no_action_filled_in_a_parameter_not_mapped_and_no_on_failure_and_the_save_stage_does_not()
    {
        var blank = Bot(Action(Text, Text), Fact(Text), new ProcedureStep { Action = " ", OnFailure = "failed" });
        var unfinished = Bot(Action(Text, Text), Fact(Text), Step(null, onFailure: null));

        Assert.Contains(
            OfProcedures(blank, ValidationStage.Version),
            mistake => mistake.Code == MistakeCodes.StepBlockEmpty && mistake.Path == "steps.run.action"
        );
        var codes = OfProcedures(unfinished, ValidationStage.Version).Select(mistake => mistake.Code).ToList();
        Assert.Contains(MistakeCodes.StepActionNoOnFailure, codes);
        Assert.Contains(MistakeCodes.StepActionUnmappedParameter, codes);
        Assert.Empty(OfProcedures(unfinished, ValidationStage.Save));
    }

    [Fact]
    [Trait("criterion", "AC-action-block-12")]
    public void A_step_reached_only_through_on_failure_is_not_unreachable()
    {
        var definition = Bot(Action(Text, Text), Fact(Text), Step());

        Assert.DoesNotContain(OfProcedures(definition, ValidationStage.Version), mistake => mistake.Code == MistakeCodes.StepUnreachable);
    }

    [Fact]
    [Trait("criterion", "AC-action-block-18")]
    public void A_local_fact_is_mapped_by_the_same_rules_of_reference_and_type()
    {
        var locals = new Dictionary<string, LocalFact>
        {
            ["note"] = new()
            {
                Type = FactTypes.Text,
                Description = "d",
                MaxLength = 10,
            },
            ["wide"] = new()
            {
                Type = FactTypes.Text,
                Description = "d",
                MaxLength = 500,
            },
        };
        BotDefinition With(string parameter, string result)
        {
            return Bot(Action(Text, Text), Fact(Text), Step()) with { Procedures = [Procedure(Step(parameter, result), locals)] };
        }

        Assert.Empty(Save(With("note", "wide")));
        Assert.Equal(
            [MistakeCodes.ActionMappingTooWide, MistakeCodes.ActionMappingTooWide],
            Save(With("wide", "note")).Select(mistake => mistake.Code)
        );
        Assert.Equal(MistakeCodes.ReferenceMissing, Assert.Single(Save(With("note", "ghost"))).Code);
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-18")]
    public void A_step_can_use_a_code_action_by_the_same_rules_of_mapping_and_types()
    {
        var fits = Bot(Action(Text, Text, code: true), Fact(Text), Step());
        var unfit = Bot(Action(Text with { MaxLength = 5 }, Text, code: true), Fact(Text), Step());

        Assert.DoesNotContain(
            OfProcedures(fits, ValidationStage.Version),
            mistake => mistake.Path.StartsWith("steps.run", StringComparison.Ordinal)
        );
        Assert.Contains(
            "parameter 'input' (text up to 5)",
            Assert.Single(OfProcedures(unfit, ValidationStage.Version), mistake => mistake.Code == MistakeCodes.ActionCodeMapping).Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-18")]
    public void A_mapping_that_does_not_fit_a_code_action_is_a_version_mistake_and_refuses_no_save()
    {
        var misfits = Bot(Action(Number, Number, code: true), Fact(Text), Step("the.fact", "the.fact"));
        var unknown = Bot(
            Action(Text, Text, code: true),
            Fact(Text),
            new ProcedureStep
            {
                Action = "check",
                With = new Dictionary<string, string> { ["gone"] = "the.fact" },
                OnFailure = "failed",
            }
        );

        Assert.Empty(Save(misfits));
        Assert.Empty(Save(unknown));
        Assert.Equal(2, OfProcedures(misfits, ValidationStage.Version).Count(mistake => mistake.Code == MistakeCodes.ActionCodeMapping));
        Assert.Contains(
            OfProcedures(unknown, ValidationStage.Version),
            mistake => mistake.Code == MistakeCodes.ActionCodeMapping && mistake.Path == "steps.run.with.gone"
        );
    }
}
