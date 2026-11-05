using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation;

public sealed class PersonaRulesTests
{
    private static readonly string TooLong = new('x', 20_001);

    private static BotDefinition With(Persona? persona) => new() { FormatVersion = 1, Persona = persona };

    private static BotDeclaration Declaring(string? persona) =>
        new()
        {
            Bot = "rzeczoznawca",
            FormatVersion = 1,
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
