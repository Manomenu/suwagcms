using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Format.Personas;
using Suwagcms.Sdk.Format.Procedures.Blocks;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation.Rules;

public sealed class PersonaRulesTests
{
    private static readonly string TooLong = new('x', 20_001);

    private static BotDefinition With(Persona? persona) => new() { FormatVersion = 2, Persona = persona };

    private static BotDeclaration Declaring(string? persona) =>
        new()
        {
            Bot = "rzeczoznawca",
            FormatVersion = 2,
            Blocks = Block.Names,
            Persona = persona,
        };

    private static List<string> Codes(BotDefinition definition, ValidationStage stage, BotDeclaration? declaration) =>
        [
            .. DefinitionValidator
                .Validate(definition, stage, declaration)
                .Select(mistake => mistake.Code)
                .Where(code => code.StartsWith("persona.", StringComparison.Ordinal)),
        ];

    [Fact]
    [Trait("criterion", "AC-bot-persona-4")]
    public void Every_text_above_the_limit_is_a_save_mistake_naming_its_field_and_a_text_at_the_limit_is_not()
    {
        var mistakes = DefinitionValidator.Validate(
            With(
                new Persona
                {
                    Personality = TooLong,
                    Scope = new PersonaScope
                    {
                        InScope = TooLong,
                        OutOfScope = new string('x', 20_000),
                        WhenOutOfScope = TooLong,
                    },
                }
            ),
            ValidationStage.Save
        );

        var tooLong = mistakes.Where(mistake => mistake.Code == MistakeCodes.PersonaTextTooLong).ToList();
        Assert.Equal(["personality", "scope.inScope", "scope.whenOutOfScope"], tooLong.Select(mistake => mistake.Path));
        Assert.All(tooLong, mistake => Assert.Equal(ValidationStage.Save, mistake.Stage));
        Assert.All(tooLong, mistake => Assert.Contains("20000", mistake.Fix, StringComparison.Ordinal));
    }

    private static readonly PersonaScope WholeScope = new()
    {
        InScope = "Valuations",
        OutOfScope = "Legal advice",
        WhenOutOfScope = "Decline",
    };

    private static readonly Persona Whole = new() { Personality = "You help.", Scope = WholeScope };

    [Fact]
    [Trait("criterion", "AC-bot-persona-15")]
    public void A_bot_that_declares_a_complete_persona_is_satisfied_by_four_filled_parts_at_version_and_not_checked_at_save()
    {
        Assert.Empty(Codes(With(Whole), ValidationStage.Version, Declaring(BotDeclaration.PersonaComplete)));
        Assert.Empty(Codes(With(null), ValidationStage.Save, Declaring(BotDeclaration.PersonaComplete)));
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-15")]
    public void A_bot_that_declares_a_complete_persona_gets_one_version_mistake_per_part_left_empty_naming_it()
    {
        var blankScope = Whole with { Scope = WholeScope with { WhenOutOfScope = " \n" } };
        var mistakes = DefinitionValidator.Validate(With(blankScope), ValidationStage.Version, Declaring(BotDeclaration.PersonaComplete));

        var mistake = Assert.Single(mistakes, found => found.Code == MistakeCodes.PersonaIncomplete);
        Assert.Equal(("scope.whenOutOfScope", ValidationStage.Version), (mistake.Path, mistake.Stage));
        Assert.Equal(
            ["personality", "scope.inScope", "scope.outOfScope", "scope.whenOutOfScope"],
            DefinitionValidator
                .Validate(With(null), ValidationStage.Version, Declaring(BotDeclaration.PersonaComplete))
                .Where(found => found.Code == MistakeCodes.PersonaIncomplete)
                .Select(found => found.Path)
        );
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-15")]
    public void A_partly_filled_persona_still_satisfies_required_and_any_third_word_is_refused()
    {
        var part = With(new Persona { Personality = "You help." });

        Assert.Empty(Codes(part, ValidationStage.Version, Declaring(BotDeclaration.PersonaRequired)));
        Assert.Equal(3, Codes(part, ValidationStage.Version, Declaring(BotDeclaration.PersonaComplete)).Count);
        Assert.Contains(
            DeclarationValidator.CheckAnswer(Declaring("full"), "rzeczoznawca"),
            mistake => mistake.Code == MistakeCodes.DeclarationPersonaUnknown
        );
        Assert.DoesNotContain(
            DeclarationValidator.CheckAnswer(Declaring(BotDeclaration.PersonaComplete), "rzeczoznawca"),
            mistake => mistake.Code == MistakeCodes.DeclarationPersonaUnknown
        );
    }

    [Theory]
    [Trait("criterion", "AC-bot-persona-13")]
    [InlineData(null)]
    [InlineData("  \n\t ")]
    public void A_bot_that_requires_a_persona_gets_a_version_mistake_when_there_is_no_text_and_none_at_save(string? text)
    {
        var definition = With(text is null ? null : new Persona { Personality = text });

        Assert.Equal([MistakeCodes.PersonaRequired], Codes(definition, ValidationStage.Version, Declaring(BotDeclaration.PersonaRequired)));
        Assert.Empty(Codes(definition, ValidationStage.Save, Declaring(BotDeclaration.PersonaRequired)));
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-13")]
    public void A_bot_that_does_not_declare_it_is_unaffected_and_a_part_of_a_persona_satisfies_one_that_does()
    {
        var none = With(null);
        var some = With(new Persona { Scope = new PersonaScope { OutOfScope = "Legal advice" } });

        Assert.Empty(Codes(none, ValidationStage.Version, Declaring(null)));
        Assert.Empty(Codes(none, ValidationStage.Version, null));
        Assert.Empty(Codes(some, ValidationStage.Version, Declaring(BotDeclaration.PersonaRequired)));
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-13")]
    public void A_declaration_saying_anything_else_about_a_persona_is_refused_with_the_word_to_use()
    {
        var mistake = Assert.Single(DeclarationValidator.CheckAnswer(Declaring("requried"), "rzeczoznawca"));

        Assert.Equal(MistakeCodes.DeclarationPersonaUnknown, mistake.Code);
        Assert.Contains("persona: required", mistake.Fix, StringComparison.Ordinal);
    }
}
