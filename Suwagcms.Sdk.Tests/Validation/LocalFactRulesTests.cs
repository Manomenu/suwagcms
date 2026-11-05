using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation;

public sealed class LocalFactRulesTests
{
    private static readonly LocalFact Kind = new()
    {
        Type = FactTypes.Enum,
        Description = "What is valued",
        Values = ["house", "flat"],
    };

    private static readonly LocalFact Rooms = new()
    {
        Type = FactTypes.WholeNumber,
        Description = "How many rooms",
        Min = 1,
        Max = 10,
    };

    private static readonly LocalFact Note = new()
    {
        Type = FactTypes.Text,
        Description = "A note",
        MaxLength = 5,
    };

    private static Procedure Procedure(
        string id,
        IReadOnlyDictionary<string, LocalFact>? locals,
        IReadOnlyDictionary<string, ProcedureStep>? steps = null
    ) =>
        new()
        {
            Id = id,
            Description = "A procedure",
            LocalFacts = locals,
            Steps = steps ?? new Dictionary<string, ProcedureStep>(),
        };

    private static IReadOnlyList<Mistake> Check(params Procedure[] procedures) =>
        DefinitionValidator.Validate(
            new BotDefinition
            {
                FormatVersion = BotDefinition.CurrentFormatVersion,
                Facts = [Samples.CityFact],
                Procedures = procedures,
            },
            ValidationStage.Save
        );

    private static ProcedureStep Collect(params string[] names) => new() { Collect = names };

    private static ProcedureStep When(string name, Condition condition) =>
        new()
        {
            Say = "Hi",
            Next = new NextSteps
            {
                Rules =
                [
                    new Transition
                    {
                        When = new Dictionary<string, Condition> { [name] = condition },
                        To = "end",
                    },
                    new Transition { To = "end" },
                ],
            },
        };

    private static Dictionary<string, ProcedureStep> Steps(ProcedureStep step) =>
        new()
        {
            ["ask"] = step,
            ["end"] = new() { Say = "Bye" },
        };

    [Fact]
    [Trait("criterion", "AC-local-facts-2")]
    public void A_local_fact_without_writableBy_has_set_and_no_or_empty_localFacts_is_accepted()
    {
        Assert.Equal(FactWriters.Set, Note.WritableBy);
        Assert.Empty(Check(Procedure("noField", null), Procedure("emptyField", new Dictionary<string, LocalFact>())));
    }

    [Theory]
    [Trait("criterion", "AC-local-facts-3")]
    [Trait("criterion", "AC-local-facts-12")]
    [InlineData("", "text", MistakeCodes.FactDescriptionMissing, "description")]
    [InlineData("A note", "blob", MistakeCodes.FactTypeInvalid, "type")]
    [InlineData("A note", "int", MistakeCodes.FactConstraintForeign, "maxLength")]
    public void A_bad_local_fact_has_the_code_of_a_global_one_and_a_path_inside_localFacts(
        string description,
        string type,
        string code,
        string field
    )
    {
        var bad = Note with { Description = description, Type = type };

        var mistake = Assert.Single(
            Check(Procedure("mine", new Dictionary<string, LocalFact> { ["note"] = bad })),
            found => found.Code == code
        );

        Assert.Equal(new ResourceRef(ResourceKind.Procedure, "mine"), mistake.Resource);
        Assert.Equal($"localFacts.note.{field}", mistake.Path);
    }

    [Theory]
    [Trait("criterion", "AC-local-facts-4")]
    [Trait("criterion", "AC-local-facts-12")]
    [InlineData("order.note", "orderNote")]
    [InlineData("Bad Name", "badName")]
    public void A_local_fact_name_with_a_dot_or_not_in_camelCase_is_refused_with_a_fix(string name, string fix)
    {
        var mistake = Assert.Single(Check(Procedure("mine", new Dictionary<string, LocalFact> { [name] = Note })));

        Assert.Equal(MistakeCodes.FactNameInvalid, mistake.Code);
        Assert.Equal($"localFacts.{name}", mistake.Path);
        Assert.Contains(fix, mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-local-facts-5")]
    [Trait("criterion", "AC-local-facts-12")]
    public void A_local_fact_with_a_global_facts_name_names_both_and_lists_every_procedure_holding_it()
    {
        var name = Samples.CityFact.Name.Replace(".", "", StringComparison.Ordinal);
        var global = Samples.CityFact with { Name = name };
        var clash = new Dictionary<string, LocalFact> { [name] = Note };

        var mistake = Assert.Single(
            DefinitionValidator.Validate(
                new BotDefinition
                {
                    FormatVersion = BotDefinition.CurrentFormatVersion,
                    Facts = [global],
                    Procedures = [Procedure("first", clash), Procedure("second", clash)],
                },
                ValidationStage.Save
            )
        );

        Assert.Equal(MistakeCodes.FactLocalNameClash, mistake.Code);
        Assert.Contains($"'{name}'", mistake.Message, StringComparison.Ordinal);
        Assert.Contains("'first', 'second'", mistake.Message, StringComparison.Ordinal);
        Assert.Equal(["first", "second"], mistake.Related.Select(related => related.Resource.Id));
    }

    [Fact]
    [Trait("criterion", "AC-local-facts-6")]
    [Trait("criterion", "AC-local-facts-12")]
    public void Steps_use_a_local_fact_by_its_short_name_under_the_rules_for_facts()
    {
        var locals = new Dictionary<string, LocalFact>
        {
            ["kind"] = Kind,
            ["rooms"] = Rooms,
            ["flag"] = Note with { WritableBy = FactWriters.Assign },
        };
        Assert.Empty(
            Check(
                Procedure(
                    "fine",
                    locals,
                    new Dictionary<string, ProcedureStep>
                    {
                        ["ask"] = Collect("kind", "rooms"),
                        ["mark"] = new() { Assign = new Dictionary<string, string> { ["flag"] = "abc" } },
                        ["check"] = When("kind", new Condition { OneOf = ["house"] }),
                        ["end"] = new() { Say = "Bye" },
                    }
                )
            )
        );

        Assert.Equal(MistakeCodes.FactCollectNotWritable, Assert.Single(Check(Procedure("bad", locals, Steps(Collect("flag"))))).Code);
        Assert.Equal(
            MistakeCodes.ConditionTypeMisfit,
            Assert.Single(Check(Procedure("bad", locals, Steps(When("kind", new Condition { Range = new NumberRange { Min = 1 } }))))).Code
        );
        Assert.Equal(
            MistakeCodes.ConditionValueInvalid,
            Assert.Single(Check(Procedure("bad", locals, Steps(When("kind", new Condition { OneOf = ["land"] }))))).Code
        );
        Assert.Equal(
            MistakeCodes.AssignValueInvalid,
            Assert
                .Single(
                    Check(
                        Procedure("bad", locals, Steps(new ProcedureStep { Assign = new Dictionary<string, string> { ["rooms"] = "99" } }))
                    )
                )
                .Code
        );
    }

    [Fact]
    [Trait("criterion", "AC-local-facts-6")]
    public void A_procedure_whose_only_collect_names_local_facts_still_needs_whenStalled_for_a_version()
    {
        var procedure = Procedure("mine", new Dictionary<string, LocalFact> { ["note"] = Note }, Steps(Collect("note")));

        var mistakes = DefinitionValidator.Validate(
            new BotDefinition { FormatVersion = BotDefinition.CurrentFormatVersion, Procedures = [procedure] },
            ValidationStage.Version
        );

        Assert.Contains(mistakes, mistake => mistake.Code == MistakeCodes.ProcedureNoWhenStalled);
    }

    [Theory]
    [Trait("criterion", "AC-local-facts-7")]
    [Trait("criterion", "AC-local-facts-12")]
    [InlineData("note")]
    [InlineData("other.note")]
    [InlineData("mine.visitDate")]
    public void A_step_reaching_a_local_fact_of_another_procedure_or_its_own_by_a_long_name_is_refused(string name)
    {
        var mine = Procedure(
            "mine",
            new Dictionary<string, LocalFact> { ["visitDate"] = Note },
            new Dictionary<string, ProcedureStep> { ["ask"] = Collect(name) }
        );
        var other = Procedure("other", new Dictionary<string, LocalFact> { ["note"] = Note });

        var mistake = Assert.Single(Check(mine, other));

        Assert.Equal(MistakeCodes.ReferenceMissing, mistake.Code);
        Assert.Contains("which this procedure does not see", mistake.Message, StringComparison.Ordinal);
        Assert.Contains($"Facts it sees: {Samples.CityFact.Name}, visitDate.", mistake.Fix, StringComparison.Ordinal);
        Assert.Contains("Local facts of other procedures are not visible.", mistake.Fix, StringComparison.Ordinal);
    }

    private static Procedure Testing(string id, LocalFact fact, string value) =>
        Procedure(
            id,
            new Dictionary<string, LocalFact> { ["same"] = fact },
            Steps(new ProcedureStep { Assign = new Dictionary<string, string> { ["same"] = value } })
        );

    [Fact]
    [Trait("criterion", "AC-local-facts-8")]
    public void Two_procedures_hold_one_name_with_different_types_and_each_is_checked_against_its_own()
    {
        Assert.Empty(Check(Testing("first", Kind, "house"), Testing("second", Rooms, "3")));

        var mistake = Assert.Single(Check(Testing("first", Kind, "house"), Testing("second", Rooms, "house")));
        Assert.Equal(MistakeCodes.AssignValueInvalid, mistake.Code);
        Assert.Equal("second", mistake.Resource.Id);
    }

    [Fact]
    [Trait("criterion", "AC-local-facts-9")]
    public void A_removed_local_fact_a_step_still_uses_is_one_refusal_listing_every_step_and_removing_its_uses_too_is_sound()
    {
        var withFact = Procedure(
            "mine",
            new Dictionary<string, LocalFact> { ["note"] = Note },
            new Dictionary<string, ProcedureStep>
            {
                ["ask"] = Collect("note"),
                ["confirm"] = When("note", new Condition { Known = true }),
                ["end"] = new() { Say = "Bye" },
            }
        );
        var without = withFact with { LocalFacts = null };
        var before = new BotDefinition { FormatVersion = BotDefinition.CurrentFormatVersion, Procedures = [withFact] };
        var after = new BotDefinition { FormatVersion = BotDefinition.CurrentFormatVersion, Procedures = [without] };

        var offline = DefinitionValidator.Validate(after, ValidationStage.Save);
        Assert.Equal(2, offline.Count);
        var mistake = Assert.Single(EditGuard.Reshape(before, after, offline));

        Assert.Equal(MistakeCodes.ReferenceMissing, mistake.Code);
        Assert.Equal("localFacts.note", mistake.Path);
        Assert.Contains("no longer declares local fact 'note'", mistake.Message, StringComparison.Ordinal);
        Assert.Contains("step ask (collect), step confirm (when)", mistake.Message, StringComparison.Ordinal);
        Assert.Equal(2, mistake.Related.Count);

        var cleaned = new BotDefinition
        {
            FormatVersion = BotDefinition.CurrentFormatVersion,
            Procedures = [without with { Steps = new Dictionary<string, ProcedureStep> { ["end"] = new() { Say = "Bye" } } }],
        };
        Assert.Empty(DefinitionValidator.Validate(cleaned, ValidationStage.Save));
    }

    [Fact]
    [Trait("criterion", "AC-local-facts-11")]
    public void The_walker_gives_no_reference_for_a_use_of_a_local_fact_but_keeps_it_among_the_uses()
    {
        var procedure = Procedure(
            "mine",
            new Dictionary<string, LocalFact> { ["note"] = Note },
            new Dictionary<string, ProcedureStep> { ["ask"] = Collect("note", "customer.city") }
        );

        Assert.Equal(["customer.city"], ReferenceWalker.From(procedure).Select(reference => reference.To.Id));
        Assert.Equal(["note", "customer.city"], ReferenceWalker.Uses(procedure).Select(reference => reference.To.Id));
    }
}
