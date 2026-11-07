using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation.Rules;

public sealed class FactRulesTests
{
    private static readonly GlobalFact Kind = new()
    {
        Name = "order.kind",
        Type = FactTypes.Enum,
        Description = "What is being valued",
        Values = ["house", "flat"],
    };

    private static readonly GlobalFact Floors = new()
    {
        Name = "order.floors",
        Type = FactTypes.WholeNumber,
        Description = "How many floors",
        Min = 1,
        Max = 40,
    };

    private static IReadOnlyList<Mistake> Check(params GlobalFact[] facts) =>
        DefinitionValidator.Validate(new BotDefinition { FormatVersion = 3, Facts = facts }, ValidationStage.Save);

    private static string[] Codes(params GlobalFact[] facts) => [.. Check(facts).Select(mistake => mistake.Code)];

    [Theory]
    [Trait("criterion", "AC-global-facts-16")]
    [InlineData("a\nb", MistakeCodes.FactEnumValueUnusable)]
    [InlineData("a\rb", MistakeCodes.FactEnumValueUnusable)]
    [InlineData("a\0b", MistakeCodes.FactEnumValueUnusable)]
    [InlineData("other", MistakeCodes.FactEnumValueReserved)]
    public void An_enum_value_with_a_line_break_or_a_NUL_or_the_reserved_other_is_refused_beside_the_value(string value, string code)
    {
        var mistake = Assert.Single(Check(Kind with { Values = ["house", value] }));

        Assert.Equal((code, "values[1]"), (mistake.Code, mistake.Path));
        Assert.False(string.IsNullOrWhiteSpace(mistake.Fix));
    }

    [Theory]
    [Trait("criterion", "AC-global-facts-16")]
    [InlineData("Other")]
    [InlineData("otherType")]
    [InlineData("an other")]
    public void Only_the_exact_value_other_is_reserved(string value) => Assert.Empty(Check(Kind with { Values = ["house", value] }));

    [Fact]
    [Trait("criterion", "AC-global-facts-1")]
    public void A_valid_enum_int_and_text_fact_are_each_accepted() => Assert.Empty(Check(Kind, Floors, Samples.CityFact));

    [Fact]
    [Trait("criterion", "AC-global-facts-2")]
    public void A_fact_without_writableBy_has_set()
    {
        Assert.Equal("set", Kind.WritableBy);
        Assert.Equal(
            "set",
            new GlobalFact
            {
                Name = "a",
                Type = "text",
                Description = "d",
            }.WritableBy
        );
    }

    [Theory]
    [Trait("criterion", "AC-global-facts-2")]
    [InlineData("assign")]
    [InlineData("set")]
    [InlineData("correct")]
    public void The_three_writableBy_levels_are_accepted(string level) => Assert.Empty(Check(Kind with { WritableBy = level }));

    [Theory]
    [Trait("criterion", "AC-global-facts-2")]
    [InlineData("")]
    [InlineData("Set")]
    [InlineData("everyone")]
    public void Any_other_writableBy_is_refused_with_the_levels_listed(string level)
    {
        var mistake = Assert.Single(Check(Kind with { WritableBy = level }));

        Assert.Equal(MistakeCodes.FactWritableByInvalid, mistake.Code);
        Assert.Equal("writableBy", mistake.Path);
        Assert.Contains("assign, set, correct", mistake.Fix, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("criterion", "AC-global-facts-3")]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_description_is_refused(string description) =>
        Assert.Equal([MistakeCodes.FactDescriptionMissing], Codes(Kind with { Description = description }));

    [Theory]
    [Trait("criterion", "AC-global-facts-3")]
    [InlineData("")]
    [InlineData("number")]
    [InlineData("Enum")]
    public void A_missing_or_unknown_type_is_refused_and_only_for_the_type(string type)
    {
        var mistake = Assert.Single(Check(Kind with { Type = type, Values = null }));

        Assert.Equal(MistakeCodes.FactTypeInvalid, mistake.Code);
        Assert.Contains("enum, int, text", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-3")]
    public void An_enum_with_fewer_than_two_values_is_refused_whether_it_has_one_or_none()
    {
        Assert.Equal([MistakeCodes.FactEnumTooFewValues], Codes(Kind with { Values = ["house"] }));
        Assert.Equal([MistakeCodes.FactEnumTooFewValues], Codes(Kind with { Values = [] }));
        Assert.Equal([MistakeCodes.FactEnumTooFewValues], Codes(Kind with { Values = null }));
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-3")]
    public void An_enum_value_that_is_blank_or_repeated_is_refused_at_its_position()
    {
        var blank = Assert.Single(Check(Kind with { Values = ["house", " ", "flat"] }));
        var repeated = Assert.Single(Check(Kind with { Values = ["house", "flat", "house"] }));

        Assert.Equal((MistakeCodes.FactEnumValueBlank, "values[1]"), (blank.Code, blank.Path));
        Assert.Equal((MistakeCodes.FactEnumValueRepeated, "values[2]"), (repeated.Code, repeated.Path));
        Assert.Equal(["values[0]"], repeated.Related.Select(related => related.Location));
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-3")]
    public void An_int_needs_both_bounds_and_min_may_not_exceed_max()
    {
        Assert.Equal([MistakeCodes.FactIntBoundMissing], Codes(Floors with { Min = null }));
        Assert.Equal([MistakeCodes.FactIntBoundMissing], Codes(Floors with { Max = null }));
        Assert.Equal([MistakeCodes.FactIntBoundMissing, MistakeCodes.FactIntBoundMissing], Codes(Floors with { Min = null, Max = null }));
        Assert.Equal([MistakeCodes.FactIntRangeInverted], Codes(Floors with { Min = 9, Max = 8 }));
        Assert.Empty(Check(Floors with { Min = 8, Max = 8 }));
    }

    [Theory]
    [Trait("criterion", "AC-global-facts-3")]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_text_needs_a_maxLength_above_zero(int? length)
    {
        Assert.Equal([MistakeCodes.FactTextMaxLengthInvalid], Codes(Samples.CityFact with { MaxLength = length }));
        Assert.Empty(Check(Samples.CityFact with { MaxLength = 1 }));
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-3")]
    public void A_constraint_of_another_type_is_refused_by_name()
    {
        var mistakes = Check(Floors with { Values = ["a", "b"], MaxLength = 3 });

        Assert.Equal(
            [(MistakeCodes.FactConstraintForeign, "maxLength"), (MistakeCodes.FactConstraintForeign, "values")],
            mistakes.Select(mistake => (mistake.Code, mistake.Path))
        );
        Assert.Equal([MistakeCodes.FactConstraintForeign], Codes(Kind with { Min = 1 }));
        Assert.Equal([MistakeCodes.FactConstraintForeign], Codes(Samples.CityFact with { Max = 1 }));
    }

    [Theory]
    [Trait("criterion", "AC-global-facts-4")]
    [InlineData("customer.city")]
    [InlineData("city")]
    [InlineData("customer.homeAddress.city2")]
    public void A_camelCase_name_with_dots_is_accepted(string name) => Assert.Empty(Check(Kind with { Name = name }));

    [Theory]
    [Trait("criterion", "AC-global-facts-4")]
    [InlineData("Customer.city", "customer.city")]
    [InlineData("customer city", "customerCity")]
    [InlineData("customer_city", "customerCity")]
    [InlineData("customer..city", "customer.name.city")]
    [InlineData(".city", "name.city")]
    [InlineData("", "name")]
    [InlineData("2nd", "n2nd")]
    public void A_name_that_is_not_camelCase_words_joined_by_dots_is_refused_with_the_name_to_use(string name, string suggested)
    {
        var mistake = Assert.Single(Check(Kind with { Name = name }));

        Assert.Equal(MistakeCodes.FactNameInvalid, mistake.Code);
        Assert.Contains($"'{suggested}'", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-4")]
    public void A_name_the_bot_already_has_is_refused_naming_both_places()
    {
        var mistake = Assert.Single(Check(Kind, Floors, Kind));

        Assert.Equal(MistakeCodes.DuplicateId, mistake.Code);
        Assert.Equal(["facts[0]", "facts[2]"], mistake.Related.Select(related => related.Location));
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-5")]
    public void A_fact_that_breaks_several_rules_gets_every_mistake_in_one_result()
    {
        var bad = new GlobalFact
        {
            Name = "Order Kind",
            Type = FactTypes.Enum,
            Description = "",
            WritableBy = "all",
            Values = ["house"],
        };

        Assert.Equal(
            [
                MistakeCodes.FactDescriptionMissing,
                MistakeCodes.FactEnumTooFewValues,
                MistakeCodes.FactNameInvalid,
                MistakeCodes.FactWritableByInvalid,
            ],
            Codes(bad).Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-5")]
    public void Every_mistake_of_a_fact_names_the_fact_and_a_path_and_a_fix()
    {
        var mistakes = Check(
            new GlobalFact
            {
                Name = "x y",
                Type = "",
                Description = " ",
            }
        );

        Assert.NotEmpty(mistakes);
        Assert.All(
            mistakes,
            mistake =>
            {
                Assert.Equal(new ResourceRef(ResourceKind.GlobalFact, "x y"), mistake.Resource);
                Assert.False(string.IsNullOrWhiteSpace(mistake.Path));
                Assert.False(string.IsNullOrWhiteSpace(mistake.Fix));
                Assert.Equal(ValidationStage.Save, mistake.Stage);
            }
        );
    }
}
