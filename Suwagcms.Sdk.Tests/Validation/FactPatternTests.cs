using System.Text.Json;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation;

public sealed class FactPatternTests
{
    // Why: copied verbatim from the appraiser bot's facts.yaml, the definition this format must carry.
    private const string EmailRegex = @"[^@\s]+@[^@\s]+\.[^@\s]+";
    private const string LandRegisterRegex = "[A-Z0-9]{4}/[0-9]{8}/[0-9]";

    private static GlobalFact Code(TextPattern? pattern) =>
        new()
        {
            Name = "order.code",
            Type = FactTypes.Text,
            Description = "The code",
            MaxLength = 10,
            Pattern = pattern,
        };

    private static TextPattern TwoCapitals { get; } = new() { Regex = "[A-Z]{2}", Means = "two capitals" };

    private static (string Code, string Path)[] Mistakes(GlobalFact fact) =>
        [
            .. DefinitionValidator
                .Validate(new BotDefinition { FormatVersion = 1, Facts = [fact] }, ValidationStage.Save)
                .Select(mistake => (mistake.Code, mistake.Path)),
        ];

    private static Mistake[] Check(BotDefinition definition) => [.. DefinitionValidator.Validate(definition, ValidationStage.Save)];

    private static BotDefinition Assigning(string value, TextPattern pattern) =>
        new()
        {
            FormatVersion = 1,
            Facts = [Code(pattern)],
            Procedures =
            [
                new Procedure
                {
                    Id = "p",
                    Description = "D",
                    FirstStep = "a",
                    Steps = new Dictionary<string, ProcedureStep>
                    {
                        ["a"] = new() { Assign = new Dictionary<string, string> { ["order.code"] = value } },
                    },
                },
            ],
        };

    private static BotDefinition Testing(string value, TextPattern pattern) =>
        new()
        {
            FormatVersion = 1,
            Facts = [Code(pattern)],
            Procedures =
            [
                new Procedure
                {
                    Id = "p",
                    Description = "D",
                    FirstStep = "a",
                    Steps = new Dictionary<string, ProcedureStep>
                    {
                        ["a"] = new()
                        {
                            Say = "Hi",
                            Next = new NextSteps
                            {
                                Rules =
                                [
                                    new Transition
                                    {
                                        When = new Dictionary<string, Condition> { ["order.code"] = new() { OneOf = [value] } },
                                        To = "b",
                                    },
                                    new Transition { To = "b" },
                                ],
                            },
                        },
                        ["b"] = new() { Say = "Bye" },
                    },
                },
            ],
        };

    [Fact]
    [Trait("criterion", "AC-fact-pattern-1")]
    public void A_global_and_a_local_fact_read_a_pattern_as_written_and_write_it_back()
    {
        var definition = BotDefinition.Read(
            """
            {"formatVersion":1,
             "facts":[{"name":"customer.email","type":"text","description":"D","maxLength":254,
               "pattern":{"regex":"[a-z]+","means":"letters","example":"abc","tellCustomer":"afterRefusal"}}],
             "procedures":[{"id":"p","description":"D",
               "localFacts":{"code":{"type":"text","description":"D","maxLength":9,"pattern":{"regex":"[0-9]+","means":"digits"}}}}]}
            """
        );

        Assert.Equal(
            new TextPattern
            {
                Regex = "[a-z]+",
                Means = "letters",
                Example = "abc",
                TellCustomer = TellCustomers.AfterRefusal,
            },
            definition.Facts[0].Pattern
        );
        var written = JsonSerializer.SerializeToNode(definition, DefinitionFormat.Options);
        Assert.Equal("[a-z]+", written?["facts"]?[0]?["pattern"]?["regex"]?.GetValue<string>());
        Assert.Equal(new TextPattern { Regex = "[0-9]+", Means = "digits" }, definition.Procedures[0].Locals["code"].Pattern);
        Assert.Equal(definition.Procedures[0].Locals["code"].Pattern, definition.Procedures[0].Locals["code"].Named("code").Pattern);
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-1")]
    public void A_fact_without_a_pattern_or_a_pattern_without_an_example_has_no_such_member_in_json()
    {
        var bare = JsonSerializer.SerializeToNode(Code(null), DefinitionFormat.Options)?.AsObject();
        var noExample = JsonSerializer.SerializeToNode(Code(TwoCapitals), DefinitionFormat.Options)?["pattern"]?.AsObject();
        var local = JsonSerializer.SerializeToNode(new LocalFact { Type = FactTypes.Text }, DefinitionFormat.Options)?.AsObject();

        Assert.False(bare?.ContainsKey("pattern"));
        Assert.False(noExample?.ContainsKey("example"));
        Assert.False(local?.ContainsKey("pattern"));
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-2")]
    public void TellCustomer_is_upfront_when_left_out_both_real_values_pass_and_any_other_is_refused_listing_them()
    {
        Assert.Equal(
            "upfront",
            BotDefinition
                .Read("""{"formatVersion":1,"facts":[{"name":"a","type":"text","description":"d","pattern":{}}]}""")
                .Facts[0]
                .Pattern?.TellCustomer
        );
        Assert.Empty(Mistakes(Code(TwoCapitals with { TellCustomer = "upfront" })));
        Assert.Empty(Mistakes(Code(TwoCapitals with { TellCustomer = "afterRefusal" })));

        foreach (var wrong in new[] { "never", "AfterRefusal", "" })
        {
            Assert.Equal(
                [(MistakeCodes.FactPatternTellCustomerInvalid, "pattern.tellCustomer")],
                Mistakes(Code(TwoCapitals with { TellCustomer = wrong }))
            );
        }
        var mistake = Assert.Single(
            Check(new BotDefinition { FormatVersion = 1, Facts = [Code(TwoCapitals with { TellCustomer = "never" })] })
        );
        Assert.Contains("upfront, afterRefusal", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-3")]
    public void A_pattern_needs_a_regex_and_words_for_it_and_an_example_it_gives_is_not_blank()
    {
        Assert.Equal([(MistakeCodes.FactPatternRegexMissing, "pattern.regex")], Mistakes(Code(TwoCapitals with { Regex = "" })));
        Assert.Equal([(MistakeCodes.FactPatternMeansMissing, "pattern.means")], Mistakes(Code(TwoCapitals with { Means = "  " })));
        Assert.Equal([(MistakeCodes.FactPatternExampleBlank, "pattern.example")], Mistakes(Code(TwoCapitals with { Example = " " })));
        Assert.Equal([(MistakeCodes.FactPatternExampleBlank, "pattern.example")], Mistakes(Code(TwoCapitals with { Example = "" })));
    }

    [Theory]
    [Trait("criterion", "AC-fact-pattern-4")]
    [InlineData("[A-Z")]
    [InlineData("(?=a)b")]
    [InlineData("(?<=a)b")]
    [InlineData(@"(a)\1")]
    [InlineData("(?>a)")]
    [InlineData("(?(a)b|c)")]
    [InlineData("a)|(?:b")]
    public void A_regex_that_does_not_compile_alone_and_whole_without_backtracking_is_refused(string regex) =>
        Assert.Equal([(MistakeCodes.FactPatternRegexInvalid, "pattern.regex")], Mistakes(Code(TwoCapitals with { Regex = regex })));

    [Theory]
    [Trait("criterion", "AC-fact-pattern-4")]
    [InlineData("[A-Z]{0,3}")]
    [InlineData("a*")]
    [InlineData("a|")]
    public void A_regex_that_matches_an_empty_text_is_refused(string regex) =>
        Assert.Equal([(MistakeCodes.FactPatternRegexMatchesEmpty, "pattern.regex")], Mistakes(Code(TwoCapitals with { Regex = regex })));

    [Theory]
    [Trait("criterion", "AC-fact-pattern-5")]
    [InlineData(EmailRegex, "anna@example.com", 254)]
    [InlineData(LandRegisterRegex, "WA1M/00012345/6", 30)]
    public void Both_real_examples_match_their_real_regexes_within_their_real_lengths(string regex, string example, int maxLength) =>
        Assert.Empty(
            Mistakes(
                Code(
                    new TextPattern
                    {
                        Regex = regex,
                        Means = "words",
                        Example = example,
                    }
                ) with
                {
                    MaxLength = maxLength,
                }
            )
        );

    [Theory]
    [Trait("criterion", "AC-fact-pattern-5")]
    [InlineData(EmailRegex, "not an address")]
    [InlineData(LandRegisterRegex, "WA1M/00012345/6 and more")]
    public void An_example_its_regex_does_not_match_as_a_whole_is_refused(string regex, string example) =>
        Assert.Equal(
            [(MistakeCodes.FactPatternExampleMismatch, "pattern.example")],
            Mistakes(
                Code(
                    new TextPattern
                    {
                        Regex = regex,
                        Means = "words",
                        Example = example,
                    }
                ) with
                {
                    MaxLength = 254,
                }
            )
        );

    [Theory]
    [Trait("criterion", "AC-fact-pattern-16")]
    [InlineData("ABC", 2, true)]
    [InlineData("AB", 2, false)]
    [InlineData("\U0001F600\U0001F600", 2, false)]
    public void An_example_longer_than_the_maximum_length_in_characters_is_refused_with_the_fix(string example, int maxLength, bool refused)
    {
        var fact = Code(
            new TextPattern
            {
                Regex = ".+",
                Means = "words",
                Example = example,
            }
        ) with
        {
            MaxLength = maxLength,
        };

        var mistakes = DefinitionValidator.Validate(new BotDefinition { FormatVersion = 1, Facts = [fact] }, ValidationStage.Save);

        if (refused)
        {
            var mistake = Assert.Single(mistakes);
            Assert.Equal((MistakeCodes.FactPatternExampleTooLong, "pattern.example"), (mistake.Code, mistake.Path));
            Assert.Contains("raise maxLength", mistake.Fix, StringComparison.Ordinal);
        }
        else
        {
            Assert.Empty(mistakes);
        }
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-16")]
    public void An_example_of_a_local_fact_longer_than_its_maximum_length_is_refused_beside_it()
    {
        var local = new LocalFact
        {
            Type = FactTypes.Text,
            Description = "D",
            MaxLength = 2,
            Pattern = new TextPattern
            {
                Regex = ".+",
                Means = "words",
                Example = "ABC",
            },
        };
        var definition = new BotDefinition
        {
            FormatVersion = 1,
            Procedures =
            [
                new Procedure
                {
                    Id = "p",
                    Description = "D",
                    FirstStep = "a",
                    LocalFacts = new Dictionary<string, LocalFact> { ["code"] = local },
                    Steps = new Dictionary<string, ProcedureStep> { ["a"] = new() { Say = "Hi" } },
                },
            ],
        };

        Assert.Contains(
            Check(definition),
            mistake => mistake is { Code: MistakeCodes.FactPatternExampleTooLong, Path: "localFacts.code.pattern.example" }
        );
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-16")]
    public void An_invalid_maximum_length_has_its_own_mistake_and_an_example_is_not_measured_against_it() =>
        Assert.Equal(
            [(MistakeCodes.FactTextMaxLengthInvalid, "maxLength")],
            Mistakes(
                Code(
                    new TextPattern
                    {
                        Regex = ".+",
                        Means = "words",
                        Example = "ABC",
                    }
                ) with
                {
                    MaxLength = 0,
                }
            )
        );

    [Fact]
    [Trait("criterion", "AC-fact-pattern-6")]
    public void A_pattern_on_an_int_or_an_enum_is_one_mistake_naming_the_text_type_even_when_the_pattern_is_broken()
    {
        var onInt = new GlobalFact
        {
            Name = "n",
            Type = FactTypes.WholeNumber,
            Description = "d",
            Min = 1,
            Max = 2,
            Pattern = new TextPattern { Regex = "(?=a)", Means = "" },
        };
        var onEnum = new GlobalFact
        {
            Name = "e",
            Type = FactTypes.Enum,
            Description = "d",
            Values = ["a", "b"],
            Pattern = TwoCapitals,
        };

        Assert.Equal([(MistakeCodes.FactConstraintForeign, "pattern")], Mistakes(onInt));
        Assert.Equal([(MistakeCodes.FactConstraintForeign, "pattern")], Mistakes(onEnum));
        Assert.Contains(
            "change the type to text",
            Assert.Single(Check(new BotDefinition { FormatVersion = 1, Facts = [onEnum] })).Fix,
            StringComparison.Ordinal
        );
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-7")]
    public void A_value_in_an_assign_or_a_condition_must_match_and_the_fix_gives_the_pattern()
    {
        var assign = Assert.Single(Check(Assigning("ab", TwoCapitals)));
        var condition = Assert.Single(Check(Testing("ab", TwoCapitals)));

        Assert.Equal(MistakeCodes.AssignValueInvalid, assign.Code);
        Assert.Contains("written as the pattern [A-Z]{2}", assign.Fix, StringComparison.Ordinal);
        Assert.Equal(MistakeCodes.ConditionValueInvalid, condition.Code);
        Assert.Empty(Check(Assigning("AB", TwoCapitals)));
        Assert.Empty(Check(Testing("AB", TwoCapitals)));
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-7")]
    public void A_broken_regex_throws_nowhere_and_its_mistake_stands_alone_beside_every_other()
    {
        var broken = TwoCapitals with { Regex = "(?=a)b" };
        var definition = Assigning("ab", broken) with { Facts = [Code(broken) with { Description = " " }] };

        Assert.Equal(
            [MistakeCodes.FactDescriptionMissing, MistakeCodes.FactPatternRegexInvalid],
            Check(definition).Select(mistake => mistake.Code).Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-7")]
    public void A_local_facts_pattern_is_judged_where_it_sits_and_its_values_must_match()
    {
        var local = new LocalFact
        {
            Type = FactTypes.Text,
            Description = "d",
            MaxLength = 5,
            Pattern = TwoCapitals with { Example = "ab" },
        };
        var definition = new BotDefinition
        {
            FormatVersion = 1,
            Procedures =
            [
                new Procedure
                {
                    Id = "p",
                    Description = "D",
                    FirstStep = "a",
                    LocalFacts = new Dictionary<string, LocalFact> { ["code"] = local },
                    Steps = new Dictionary<string, ProcedureStep>
                    {
                        ["a"] = new() { Assign = new Dictionary<string, string> { ["code"] = "ab" } },
                    },
                },
            ],
        };

        var mistakes = Check(definition);

        Assert.Contains(
            mistakes,
            mistake => mistake is { Code: MistakeCodes.FactPatternExampleMismatch, Path: "localFacts.code.pattern.example" }
        );
        Assert.Contains(mistakes, mistake => mistake.Code == MistakeCodes.AssignValueInvalid);
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-8")]
    public void An_edit_of_a_pattern_a_use_no_longer_satisfies_has_one_cause_naming_the_value_and_the_steps()
    {
        var before = Assigning("AB", TwoCapitals);
        var after = before with { Facts = [Code(TwoCapitals with { Regex = "[0-9]{2}" })] };

        var reshaped = EditGuard.Reshape(before, after, Check(after));

        var mistake = Assert.Single(reshaped);
        Assert.Equal((MistakeCodes.FactPatternInUse, "pattern.regex"), (mistake.Code, mistake.Path));
        Assert.Contains("'AB'", mistake.Message, StringComparison.Ordinal);
        Assert.Contains("procedure p, step a", mistake.Message, StringComparison.Ordinal);
        Assert.Equal(["p"], mistake.Related.Select(related => related.Resource.Id));
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-8")]
    public void Adding_a_pattern_a_use_does_not_satisfy_is_refused_and_a_change_every_use_satisfies_is_not()
    {
        var plain = Assigning("AB", TwoCapitals) with { Facts = [Code(null)] };
        var refused = plain with { Facts = [Code(TwoCapitals with { Regex = "[0-9]{2}" })] };
        var kept = plain with { Facts = [Code(TwoCapitals with { Regex = "[A-F]{2}" })] };

        Assert.Equal(MistakeCodes.FactPatternInUse, Assert.Single(EditGuard.Reshape(plain, refused, Check(refused))).Code);
        Assert.Empty(EditGuard.Reshape(plain, kept, Check(kept)));
    }

    [Theory]
    [Trait("criterion", "AC-fact-pattern-9")]
    [InlineData("anna@example.com", true)]
    [InlineData("anna@example", false)]
    [InlineData(" anna@example.com", false)]
    [InlineData("anna@example.com\n", false)]
    public void FactValues_tells_a_bot_whether_a_whole_value_fits_the_regex(string value, bool accepted) =>
        Assert.Equal(
            accepted,
            FactValues.Accepts(Code(new TextPattern { Regex = EmailRegex, Means = "w" }) with { MaxLength = 254 }, value)
        );

    [Fact]
    [Trait("criterion", "AC-fact-pattern-9")]
    public void FactValues_says_what_a_text_fact_takes_with_its_pattern_and_keeps_length_and_no_pattern_as_they_were()
    {
        Assert.Equal(
            "Use a non-empty text of at most 10 characters, written as the pattern [A-Z]{2}.",
            FactValues.Valid(Code(TwoCapitals))
        );
        Assert.Equal("Use a non-empty text of at most 10 characters.", FactValues.Valid(Code(null)));
        Assert.False(FactValues.Accepts(Code(new TextPattern { Regex = "[a-z]+", Means = "w" }), new string('a', 11)));
        Assert.True(FactValues.Accepts(Code(null), "anything"));
        Assert.False(FactValues.Accepts(Code(null), ""));
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-9")]
    public void FactValues_does_not_throw_on_a_broken_regex_and_leaves_the_verdict_to_its_mistake() =>
        Assert.True(FactValues.Accepts(Code(TwoCapitals with { Regex = "(?=a)b" }), "ab"));
}
