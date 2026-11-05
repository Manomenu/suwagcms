using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation;

public sealed class DeclarationValidatorTests
{
    private static BotDeclaration Good(params string[] blocks) =>
        new()
        {
            Bot = "rzeczoznawca",
            FormatVersion = 1,
            Blocks = blocks.Length == 0 ? Block.Names : blocks,
        };

    private static BotDefinition Definition =>
        new()
        {
            FormatVersion = 1,
            Facts = ProcedureSamples.Facts,
            Procedures =
            [
                ProcedureSamples.All,
                new Procedure
                {
                    Id = Procedure.ConversationId,
                    Description = "The base of every conversation",
                    FirstStep = "hello",
                    Steps = new Dictionary<string, ProcedureStep> { ["hello"] = new() { Say = "Hello" } },
                },
            ],
        };

    private static readonly System.Text.Json.JsonSerializerOptions Web = new(System.Text.Json.JsonSerializerDefaults.Web);

    private static string Json(BotDefinition definition) => System.Text.Json.JsonSerializer.Serialize(definition, Web);

    private static string[] Codes(IEnumerable<Mistake> mistakes) =>
        [.. mistakes.Select(mistake => mistake.Code).Order(StringComparer.Ordinal)];

    [Fact]
    [Trait("criterion", "AC-bot-declaration-7")]
    public void An_answer_with_every_kind_of_mistake_lists_all_of_them_at_once()
    {
        CodeActionField[] badField =
        [
            new()
            {
                Name = "kind",
                Type = "blob",
                Description = "The kind",
            },
        ];
        var answer = new BotDeclaration
        {
            Bot = "another",
            FormatVersion = 1,
            Blocks = ["listen", "fly"],
            CodeActions =
            [
                new() { Id = "lookupPolicy", Description = " " },
                new() { Id = "twice", Description = "One" },
                new() { Id = "twice", Description = "Two" },
                new()
                {
                    Id = "typed",
                    Description = "Has a bad type",
                    Results = badField,
                },
            ],
        };

        var mistakes = DeclarationValidator.CheckAnswer(answer, "rzeczoznawca");

        Assert.Equal(
            [
                MistakeCodes.CodeActionDescriptionMissing,
                MistakeCodes.CodeActionRepeated,
                MistakeCodes.DeclarationBlockUnknown,
                MistakeCodes.DeclarationBot,
                MistakeCodes.FactTypeInvalid,
            ],
            Codes(mistakes)
        );
        Assert.All(
            mistakes,
            mistake =>
            {
                Assert.Equal(ValidationStage.Version, mistake.Stage);
                Assert.False(string.IsNullOrWhiteSpace(mistake.Message));
                Assert.False(string.IsNullOrWhiteSpace(mistake.Fix));
                Assert.False(string.IsNullOrWhiteSpace(mistake.Path));
            }
        );
        Assert.Contains(mistakes, mistake => mistake.Path == "results.kind.type" && mistake.Resource.Id == "typed");
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-7")]
    public void An_answer_with_no_blocks_no_format_or_a_code_action_with_no_id_is_refused()
    {
        var answer = new BotDeclaration { Bot = "rzeczoznawca", CodeActions = [new() { Description = "Nameless" }] };

        Assert.Equal(
            [MistakeCodes.CodeActionIdMissing, MistakeCodes.DeclarationBlocksMissing, MistakeCodes.DeclarationFormatMissing],
            Codes(DeclarationValidator.CheckAnswer(answer, "rzeczoznawca"))
        );
    }

    [Theory]
    [InlineData("{ not json", "$")]
    [InlineData("""{"bot":"x","blocks":null}""", "$.blocks")]
    [InlineData("""{"bot":"x","formatVersion":"one"}""", "$.formatVersion")]
    [InlineData("""{"bot":"x","codeActions":[null]}""", "codeActions")]
    [Trait("criterion", "AC-bot-declaration-7")]
    public void An_answer_that_does_not_fit_the_contract_is_a_mistake_and_never_throws(string json, string path)
    {
        var (declaration, mistakes) = DeclarationValidator.ParseJson(json);

        Assert.Null(declaration);
        var mistake = Assert.Single(mistakes);
        Assert.Equal(MistakeCodes.DeclarationInvalid, mistake.Code);
        Assert.Equal(path, mistake.Path);
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-8")]
    public void The_blocks_of_a_format_the_cms_does_not_read_are_not_judged()
    {
        var answer = new BotDeclaration
        {
            Bot = "rzeczoznawca",
            FormatVersion = 2,
            Blocks = ["fly"],
        };

        Assert.Empty(DeclarationValidator.CheckAnswer(answer, "rzeczoznawca"));
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-10")]
    public void An_answer_that_declares_nothing_is_good_with_no_blocks_but_not_from_another_bot()
    {
        var answer = BotDeclaration.Of("rzeczoznawca", null);

        Assert.True(answer.DeclaresNothing);
        Assert.Empty(DeclarationValidator.CheckAnswer(answer, "rzeczoznawca"));
        Assert.Equal([MistakeCodes.DeclarationBot], Codes(DeclarationValidator.CheckAnswer(answer, "another")));
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-12")]
    public void Another_format_version_is_a_version_mistake_naming_both_with_its_fix_and_nothing_at_save()
    {
        var declaration = Good() with { FormatVersion = 2 };

        var mistake = Assert.Single(DefinitionValidator.Validate(Definition, ValidationStage.Version, declaration));

        Assert.Equal(MistakeCodes.DeclarationFormatVersion, mistake.Code);
        Assert.Equal(ValidationStage.Version, mistake.Stage);
        Assert.Equal("This definition is format 1 and the bot wants format 2.", mistake.Message);
        Assert.Contains("declare format 1 in the bot", mistake.Fix, StringComparison.Ordinal);
        Assert.Empty(DefinitionValidator.Validate(Definition, ValidationStage.Save, declaration));
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-17")]
    public void A_step_whose_block_the_bot_does_not_declare_names_the_procedure_the_step_and_what_it_declares_and_never_stops_a_save()
    {
        var declaration = Good("listen", "say", "assign");

        var mistakes = DefinitionValidator.Validate(Definition, ValidationStage.Version, declaration);

        var collect = Assert.Single(mistakes);
        Assert.Equal(MistakeCodes.DeclarationBlock, collect.Code);
        Assert.Equal(new ResourceRef(ResourceKind.Procedure, "orderValuation"), collect.Resource);
        Assert.EndsWith(".collect", collect.Path, StringComparison.Ordinal);
        Assert.Contains("procedure 'orderValuation'", collect.Message, StringComparison.Ordinal);
        Assert.Contains("uses block 'collect'", collect.Message, StringComparison.Ordinal);
        Assert.Contains("It declares: listen, say, assign.", collect.Message, StringComparison.Ordinal);
        Assert.Empty(DefinitionValidator.Validate(Definition, ValidationStage.Save, declaration));
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-13")]
    public void Given_no_declaration_or_one_that_declares_nothing_the_mistakes_are_the_same_as_without_one()
    {
        var unfinished = Definition with { Procedures = [] };
        var without = DefinitionValidator.Validate(unfinished, ValidationStage.Version);

        Assert.NotEmpty(without);
        Assert.Equal(without, DefinitionValidator.Validate(unfinished, ValidationStage.Version, (BotDeclaration?)null));
        Assert.Equal(without, DefinitionValidator.Validate(unfinished, ValidationStage.Version, BotDeclaration.Of("rzeczoznawca", null)));
        Assert.Equal(
            DefinitionValidator.ValidateJson(Json(unfinished), ValidationStage.Version),
            DefinitionValidator.ValidateJson(Json(unfinished), ValidationStage.Version, (BotDeclaration?)null)
        );
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-17")]
    public void The_json_form_says_the_same_as_the_records_and_an_unreadable_document_stays_one_mistake()
    {
        var declaration = Good("listen", "say", "assign");

        Assert.Equal(
            Codes(DefinitionValidator.Validate(Definition, ValidationStage.Version, declaration)),
            Codes(DefinitionValidator.ValidateJson(Json(Definition), ValidationStage.Version, declaration))
        );
        Assert.Equal(
            [MistakeCodes.FormatInvalid],
            Codes(DefinitionValidator.ValidateJson("{ not json", ValidationStage.Version, declaration))
        );
    }
}
