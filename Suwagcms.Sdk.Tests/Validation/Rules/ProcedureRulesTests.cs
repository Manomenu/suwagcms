using System.Text.Json;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.Procedures.Blocks;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation.Rules;

public sealed class ProcedureRulesTests
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static IReadOnlyList<Mistake> Check(Procedure procedure, ValidationStage stage = ValidationStage.Save) =>
        DefinitionValidator.Validate(ProcedureSamples.Bot(procedure), stage);

    private static string[] Codes(Procedure procedure, ValidationStage stage = ValidationStage.Save) =>
        [.. Check(procedure, stage).Select(mistake => mistake.Code)];

    private static Procedure Step(string name, ProcedureStep step) => ProcedureSamples.WithStep(name, step);

    [Fact]
    [Trait("criterion", "AC-procedure-model-1")]
    public void A_procedure_with_all_four_blocks_and_rule_based_next_is_accepted_and_survives_json()
    {
        var options = Wire;
        var json = JsonSerializer.Serialize(ProcedureSamples.All, options);
        var back = JsonSerializer.Deserialize<Procedure>(json, options);
        Assert.NotNull(back);
        var ask = back.Steps["ask"];
        Assert.NotNull(ask.Next);
        Assert.NotNull(ask.Next.Rules);

        Assert.Empty(Check(ProcedureSamples.All, ValidationStage.Save));
        Assert.Equal(
            ["listen", "collect", "assign", "say"],
            ProcedureSamples.All.Steps.Values.SelectMany(step => step.Blocks).Select(block => block.Name).Distinct()
        );
        Assert.Equal(JsonSerializer.Serialize(ProcedureSamples.All, options), JsonSerializer.Serialize(back, options));
        Assert.Equal(["mark", "mark", "goodbye"], ask.Next.Rules.Select(rule => rule.To));
    }

    [Theory]
    [Trait("criterion", "AC-procedure-model-3")]
    [InlineData("")]
    [InlineData("  ")]
    public void A_procedure_without_a_description_is_refused_with_a_fix(string description)
    {
        var mistake = Assert.Single(Check(ProcedureSamples.All with { Description = description }));

        Assert.Equal(MistakeCodes.ProcedureDescriptionMissing, mistake.Code);
        Assert.Equal("description", mistake.Path);
        Assert.Contains("when the procedure is used", mistake.Fix, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("criterion", "AC-procedure-model-3")]
    [InlineData("Order valuation", "orderValuation")]
    [InlineData("order_valuation", "orderValuation")]
    [InlineData("order.valuation", "orderValuation")]
    [InlineData("2nd", "n2nd")]
    public void A_procedure_id_that_is_not_one_camelCase_word_is_refused_with_the_id_to_use(string id, string suggested)
    {
        var mistake = Assert.Single(Check(ProcedureSamples.All with { Id = id }));

        Assert.Equal(MistakeCodes.ProcedureIdInvalid, mistake.Code);
        Assert.Contains($"'{suggested}'", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-3")]
    public void A_step_name_that_is_not_camelCase_is_refused_with_the_name_to_use()
    {
        var mistake = Assert.Single(Check(Step("Say bye", new ProcedureStep { Say = "Bye" })));

        Assert.Equal((MistakeCodes.StepNameInvalid, "steps.Say bye"), (mistake.Code, mistake.Path));
        Assert.Contains("'sayBye'", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-3")]
    public void An_id_the_bot_already_has_is_refused_naming_both_places()
    {
        var mistake = Assert.Single(
            DefinitionValidator.Validate(ProcedureSamples.Bot(ProcedureSamples.All, ProcedureSamples.All), ValidationStage.Save)
        );

        Assert.Equal(MistakeCodes.DuplicateId, mistake.Code);
        Assert.Equal(["procedures[0]", "procedures[1]"], mistake.Related.Select(related => related.Location));
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-4")]
    public void An_unknown_block_is_refused_with_the_blocks_that_exist()
    {
        var extra = new Dictionary<string, JsonElement> { ["speak"] = JsonDocument.Parse("\"hi\"").RootElement };
        var mistake = Assert.Single(Check(Step("talk", new ProcedureStep { UnknownBlocks = extra })));

        Assert.Equal((MistakeCodes.StepBlockUnknown, "steps.talk.speak"), (mistake.Code, mistake.Path));
        Assert.Contains("listen, collect, say, assign", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-4")]
    public void A_step_with_two_blocks_is_refused_naming_them_and_the_blocks_that_exist()
    {
        var mistake = Assert.Single(Check(Step("both", new ProcedureStep { Listen = new ListenArguments(), Say = "Hi" })));

        Assert.Equal(MistakeCodes.StepBlockMultiple, mistake.Code);
        Assert.Contains("listen, say", mistake.Message, StringComparison.Ordinal);
        Assert.Contains("listen, collect, say, assign", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-5")]
    public void A_firstStep_next_or_to_naming_a_missing_step_is_refused_with_the_steps_of_the_procedure()
    {
        var byNext = Step(
            "bad",
            new ProcedureStep
            {
                Say = "x",
                Next = new NextSteps { Step = "ghost" },
            }
        );
        var byTo = Step(
            "bad",
            new ProcedureStep
            {
                Say = "x",
                Next = new NextSteps { Rules = [new Transition { To = "ghost" }] },
            }
        );
        var byFirst = ProcedureSamples.All with { FirstStep = "ghost" };

        var found = new[] { byNext, byTo, byFirst }.Select(procedure => Assert.Single(Check(procedure))).ToList();

        Assert.All(found, mistake => Assert.Equal(MistakeCodes.StepMissing, mistake.Code));
        Assert.All(found, mistake => Assert.Matches("^Steps of 'orderValuation': .*ask, .*goodbye", mistake.Fix));
        Assert.Equal(["steps.bad.next", "steps.bad.next[0].to", "firstStep"], found.Select(mistake => mistake.Path));
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-5")]
    [Trait("criterion", "AC-procedure-creator-44")]
    public void A_rule_without_a_target_is_refused_as_naming_no_step()
    {
        var mistake = Assert.Single(
            Check(
                Step(
                    "bad",
                    new ProcedureStep
                    {
                        Say = "x",
                        Next = new NextSteps { Rules = [new Transition()] },
                    }
                )
            )
        );

        Assert.Equal(MistakeCodes.StepMissing, mistake.Code);
        Assert.Equal("Add a step to this branch, or a Next: row.", mistake.Fix);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-6")]
    public void A_collect_assign_or_when_naming_a_fact_the_bot_does_not_have_is_refused_with_the_facts_of_the_bot()
    {
        var procedure = Samples.Procedure("p") with
        {
            FirstStep = "a",
            Steps = new Dictionary<string, ProcedureStep>
            {
                ["a"] = new() { Collect = ["customer.cty"] },
                ["b"] = new() { Assign = new Dictionary<string, string> { ["nowhere"] = "x" } },
                ["c"] = new()
                {
                    Say = "x",
                    Next = new NextSteps
                    {
                        Rules =
                        [
                            new Transition
                            {
                                When = new Dictionary<string, Condition> { ["gone"] = new() { Known = true } },
                                To = "a",
                            },
                            new Transition { To = "a" },
                        ],
                    },
                },
            },
        };

        var found = Check(procedure).Where(mistake => mistake.Code == MistakeCodes.ReferenceMissing).ToList();

        Assert.Equal(["steps.a.collect[0]", "steps.b.assign.nowhere", "steps.c.next[0].when.gone"], found.Select(mistake => mistake.Path));
        Assert.Contains(
            "'collect' of step 'a' in 'p' names fact 'customer.cty', which this procedure does not see",
            found[0].Message,
            StringComparison.Ordinal
        );
        Assert.All(
            found,
            mistake => Assert.Contains("customer.city, order.floors, order.kind, system.flag", mistake.Fix, StringComparison.Ordinal)
        );
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-6")]
    public void A_bot_with_no_facts_says_to_add_one_first()
    {
        var mistakes = DefinitionValidator.Validate(
            new BotDefinition { FormatVersion = 2, Procedures = [ProcedureSamples.All] },
            ValidationStage.Save
        );

        Assert.All(
            mistakes.Where(mistake => mistake.Code == MistakeCodes.ReferenceMissing),
            mistake => Assert.Contains("no facts yet", mistake.Fix, StringComparison.Ordinal)
        );
        Assert.NotEmpty(mistakes);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-7")]
    public void A_collect_naming_a_fact_with_writableBy_assign_is_refused_with_the_fix_of_the_readme()
    {
        var mistake = Assert.Single(
            Check(Step("ask", new ProcedureStep { Collect = ["system.flag"] })),
            found => found.Code == MistakeCodes.FactCollectNotWritable
        );

        Assert.Equal("steps.ask.collect[0]", mistake.Path);
        Assert.Contains("names fact 'system.flag', which only an assign step may write", mistake.Message, StringComparison.Ordinal);
        Assert.Equal("Remove it from collect, or give the fact 'writableBy: set'.", mistake.Fix);
        Assert.Equal(new ResourceRef(ResourceKind.GlobalFact, "system.flag"), Assert.Single(mistake.Related).Resource);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-7")]
    public void An_assign_may_write_a_fact_with_writableBy_assign() =>
        Assert.DoesNotContain(MistakeCodes.FactCollectNotWritable, Codes(ProcedureSamples.All));

    private static ProcedureStep When(string fact, Condition condition) =>
        new()
        {
            Say = "x",
            Next = new NextSteps
            {
                Rules =
                [
                    new Transition
                    {
                        When = new Dictionary<string, Condition> { [fact] = condition },
                        To = "confirm",
                    },
                    new Transition { To = "confirm" },
                ],
            },
        };

    [Fact]
    [Trait("criterion", "AC-procedure-model-8")]
    public void A_range_on_a_fact_that_is_not_an_int_is_refused_with_the_conditions_that_fit()
    {
        var mistake = Assert.Single(Check(Step("pick", When("order.kind", new Condition { Range = new NumberRange { Min = 1 } }))));

        Assert.Equal((MistakeCodes.ConditionTypeMisfit, "steps.pick.next[0].when.order.kind.range"), (mistake.Code, mistake.Path));
        Assert.Contains("an enum fact: oneOf, known", mistake.Fix, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("criterion", "AC-procedure-model-8")]
    [InlineData("order.kind", "castle", "Use one of: house, flat.")]
    [InlineData("order.floors", "many", "Use a whole number from 1 to 40.")]
    [InlineData("order.floors", "41", "Use a whole number from 1 to 40.")]
    [InlineData("customer.city", "", "Use a non-empty text of at most 80 characters.")]
    public void A_oneOf_value_the_fact_can_never_take_is_refused_with_what_is_valid(string fact, string value, string fix)
    {
        var mistake = Assert.Single(Check(Step("pick", When(fact, new Condition { OneOf = [value] }))));

        Assert.Equal((MistakeCodes.ConditionValueInvalid, $"steps.pick.next[0].when.{fact}.oneOf[0]"), (mistake.Code, mistake.Path));
        Assert.Equal(fix, mistake.Fix);
    }

    [Theory]
    [Trait("criterion", "AC-procedure-model-8")]
    [InlineData(null, null)]
    [InlineData(9, 3)]
    [InlineData(50, null)]
    [InlineData(null, 0)]
    [InlineData(5, 200)]
    [InlineData(0, 5)]
    public void A_range_with_a_bound_the_fact_cannot_take_is_refused(int? min, int? max)
    {
        var mistake = Assert.Single(
            Check(
                Step(
                    "pick",
                    When(
                        "order.floors",
                        new Condition
                        {
                            Range = new NumberRange { Min = min, Max = max },
                        }
                    )
                )
            )
        );

        Assert.Equal(MistakeCodes.ConditionValueInvalid, mistake.Code);
        Assert.Equal("Use a whole number from 1 to 40.", mistake.Fix);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-8")]
    public void A_condition_with_no_kind_or_two_kinds_is_refused_and_one_kind_is_not()
    {
        var none = Assert.Single(Check(Step("pick", When("order.kind", new Condition()))));
        var two = Assert.Single(Check(Step("pick", When("order.kind", new Condition { Known = true, OneOf = ["house"] }))));

        Assert.All([none, two], mistake => Assert.Equal(MistakeCodes.ConditionKindInvalid, mistake.Code));
        Assert.Empty(Check(Step("pick", When("order.kind", new Condition { Known = false }))));
    }

    [Theory]
    [Trait("criterion", "AC-procedure-model-8")]
    [InlineData("order.floors", "99", "Use a whole number from 1 to 40.")]
    [InlineData("order.kind", "castle", "Use one of: house, flat.")]
    public void An_assign_value_the_fact_can_never_take_is_refused_with_what_is_valid(string fact, string value, string fix)
    {
        var mistake = Assert.Single(Check(Step("set", new ProcedureStep { Assign = new Dictionary<string, string> { [fact] = value } })));

        Assert.Equal((MistakeCodes.AssignValueInvalid, $"steps.set.assign.{fact}"), (mistake.Code, mistake.Path));
        Assert.Equal(fix, mistake.Fix);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-9")]
    public void The_version_stage_reports_every_unfinished_thing_of_a_procedure()
    {
        var unfinished = new Procedure
        {
            Id = "tidy",
            Description = "d",
            Steps = new Dictionary<string, ProcedureStep>
            {
                ["none"] = new(),
                ["emptyCollect"] = new() { Collect = [] },
                ["emptyAssign"] = new() { Assign = new Dictionary<string, string>() },
                ["blank"] = new() { Say = " " },
                ["rules"] = new()
                {
                    Collect = ["order.kind"],
                    Next = new NextSteps
                    {
                        Rules =
                        [
                            new Transition
                            {
                                When = new Dictionary<string, Condition> { ["order.kind"] = new() { Known = true } },
                                To = "none",
                            },
                        ],
                    },
                },
            },
            WhenStalled = new StallInstruction { Instruction = " " },
        };
        var withFirst = unfinished with { FirstStep = "rules", WhenStalled = null };

        Assert.Empty(Check(unfinished));
        Assert.Equal(
            [
                MistakeCodes.BotConversationMissing,
                MistakeCodes.NextDefaultMissing,
                MistakeCodes.ProcedureFirstStepMissing,
                MistakeCodes.ProcedureInstructionBlank,
                MistakeCodes.StepBlockEmpty,
                MistakeCodes.StepBlockEmpty,
                MistakeCodes.StepBlockEmpty,
                MistakeCodes.StepBlockMissing,
            ],
            Codes(unfinished, ValidationStage.Version).Order(StringComparer.Ordinal)
        );
        Assert.Equal(
            [
                MistakeCodes.BotConversationMissing,
                MistakeCodes.NextDefaultMissing,
                MistakeCodes.ProcedureNoWhenStalled,
                MistakeCodes.StepBlockEmpty,
                MistakeCodes.StepBlockEmpty,
                MistakeCodes.StepBlockEmpty,
                MistakeCodes.StepBlockMissing,
                MistakeCodes.StepUnreachable,
                MistakeCodes.StepUnreachable,
                MistakeCodes.StepUnreachable,
            ],
            Codes(withFirst, ValidationStage.Version).Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-9")]
    public void A_bot_with_no_conversation_procedure_is_not_ready_for_a_version_and_one_with_it_is()
    {
        var without = DefinitionValidator.Validate(ProcedureSamples.Bot(ProcedureSamples.All), ValidationStage.Version);
        var with = DefinitionValidator.Validate(
            ProcedureSamples.Bot(ProcedureSamples.All with { Id = "conversation" }),
            ValidationStage.Version
        );

        var mistake = Assert.Single(without);
        Assert.Equal((MistakeCodes.BotConversationMissing, ValidationStage.Version), (mistake.Code, mistake.Stage));
        Assert.Contains("'conversation'", mistake.Fix, StringComparison.Ordinal);
        Assert.Contains("one listen step whose next returns to itself", mistake.Fix, StringComparison.Ordinal);
        Assert.Empty(with);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-9")]
    public void A_conversation_of_one_listen_step_that_returns_to_itself_has_no_mistake_at_version()
    {
        var conversation = new Procedure
        {
            Id = Procedure.ConversationId,
            Description = "The base of every conversation",
            FirstStep = "wait",
            Steps = new Dictionary<string, ProcedureStep>
            {
                ["wait"] = new()
                {
                    Listen = new ListenArguments(),
                    Next = new NextSteps { Step = "wait" },
                },
            },
        };

        Assert.Empty(DefinitionValidator.Validate(ProcedureSamples.Bot(conversation), ValidationStage.Version));
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-9")]
    public void The_unreachable_step_is_named_with_the_fix()
    {
        var procedure = ProcedureSamples.WithStep("lonely", new ProcedureStep { Say = "Nobody comes here" });

        var mistake = Assert.Single(Check(procedure, ValidationStage.Version), found => found.Code == MistakeCodes.StepUnreachable);

        Assert.Equal("steps.lonely", mistake.Path);
        Assert.Contains("No path from firstStep reaches step 'lonely'", mistake.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-10")]
    [Trait("criterion", "AC-sdk-validator-5")]
    public void A_procedure_with_only_version_mistakes_has_none_at_save_and_each_at_version_with_its_stage()
    {
        var unfinished = ProcedureSamples.All with { FirstStep = null };

        var atSave = Check(unfinished, ValidationStage.Save);
        var atVersion = Check(unfinished, ValidationStage.Version);

        Assert.Empty(atSave);
        Assert.Equal(
            [
                (MistakeCodes.BotConversationMissing, ValidationStage.Version),
                (MistakeCodes.ProcedureFirstStepMissing, ValidationStage.Version),
            ],
            atVersion.Select(mistake => (mistake.Code, mistake.Stage)).Order()
        );
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-11")]
    public void A_loop_between_steps_and_a_conversation_returning_to_its_own_listen_make_no_mistake()
    {
        var conversation = ProcedureSamples.All with { Id = "conversation" };
        var selfLoop = Step(
            "again",
            new ProcedureStep
            {
                Say = "Again",
                Next = new NextSteps { Step = "again" },
            }
        );

        Assert.Equal("wait", conversation.Steps["confirm"].Next?.Step);
        Assert.Empty(DefinitionValidator.Validate(ProcedureSamples.Bot(conversation), ValidationStage.Version));
        Assert.DoesNotContain(MistakeCodes.StepMissing, Codes(selfLoop, ValidationStage.Version));
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-9")]
    public void Every_mistake_of_a_procedure_names_the_procedure_a_path_and_a_fix()
    {
        var mistakes = DefinitionValidator.ValidateJson(ProcedureSamples.EveryMistake, ValidationStage.Version);

        Assert.All(
            mistakes.Where(mistake => mistake.Resource.Kind == ResourceKind.Procedure),
            mistake =>
            {
                Assert.False(string.IsNullOrWhiteSpace(mistake.Path));
                Assert.False(string.IsNullOrWhiteSpace(mistake.Fix));
            }
        );
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-7")]
    public void The_reference_walker_yields_every_fact_a_procedure_names_and_how()
    {
        var references = ReferenceWalker.From(ProcedureSamples.Bot(ProcedureSamples.All)).ToList();

        Assert.All(references, reference => Assert.Equal(new ResourceRef(ResourceKind.Procedure, "orderValuation"), reference.From));
        Assert.Equal(
            new (string, string, FactUse)[]
            {
                ("steps.ask.collect[0]", "customer.city", FactUse.Collect),
                ("steps.ask.collect[1]", "order.kind", FactUse.Collect),
                ("steps.ask.next[0].when.order.kind", "order.kind", FactUse.Condition),
                ("steps.ask.next[1].when.order.floors", "order.floors", FactUse.Condition),
                ("steps.ask.next[1].when.customer.city", "customer.city", FactUse.Condition),
                ("steps.mark.assign.system.flag", "system.flag", FactUse.Assign),
            }.Order(),
            references.Select(reference => (reference.Path, reference.To.Id, reference.Use.GetValueOrDefault())).Order()
        );
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-17")]
    public void A_rule_with_no_when_before_the_last_says_the_rules_after_it_never_apply()
    {
        var step = new ProcedureStep
        {
            Say = "Hi",
            Next = new NextSteps
            {
                Rules =
                [
                    new Transition { To = "wait" },
                    new Transition
                    {
                        When = new Dictionary<string, Condition> { ["order.kind"] = new() { Known = true } },
                        To = "wait",
                    },
                    new Transition { To = "wait" },
                ],
            },
        };

        var mistake = Assert.Single(
            DefinitionValidator.Validate(
                ProcedureSamples.Bot(ProcedureSamples.WithStep("hello", step, "hello"), Samples.Procedure(Procedure.ConversationId)),
                ValidationStage.Version
            ),
            found => found.Code == MistakeCodes.NextDefaultNotLast
        );

        Assert.Equal("steps.hello.next[0]", mistake.Path);
    }
}
