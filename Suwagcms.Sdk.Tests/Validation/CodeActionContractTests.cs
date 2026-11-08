using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Actions;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.Procedures.Blocks;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation;

public sealed class CodeActionContractTests
{
    private static readonly CodeActionField Pesel = new()
    {
        Name = "pesel",
        Type = FactTypes.Text,
        Description = "The PESEL",
        MaxLength = 11,
    };

    private static readonly CodeAction Lookup = new()
    {
        Id = "lookupPolicy",
        Description = "Finds a policy",
        Parameters = [Pesel],
    };

    private static BotDeclaration Declaring(params CodeAction[] actions) =>
        new()
        {
            Bot = "b",
            FormatVersion = 4,
            Blocks = [.. Block.Names],
            CodeActions = actions,
        };

    private static BotDefinition Holding(params BotAction[] actions) => new() { FormatVersion = 4, Actions = actions };

    private static BotAction Http(string id) =>
        new()
        {
            Id = id,
            Description = "d",
            Implementation = new ActionImplementation
            {
                Http = new HttpCall { Method = "GET", Address = "https://x.internal" },
            },
        };

    private static string[] Codes(BotDefinition definition, BotDeclaration declaration, ValidationStage stage) =>
        [
            .. DefinitionValidator
                .Validate(definition, stage, declaration)
                .Where(mistake => mistake.Code.StartsWith("action.code.", StringComparison.Ordinal))
                .Select(mistake => mistake.Code),
        ];

    [Fact]
    [Trait("criterion", "AC-code-actions-10")]
    public void The_version_stage_reports_a_copy_the_declaration_lacks_with_a_mistake_and_the_save_stage_does_not()
    {
        var definition = Holding(Lookup.ToAction());

        Assert.Equal([MistakeCodes.ActionCodeUndeclared], Codes(definition, Declaring(), ValidationStage.Version));
        Assert.Empty(Codes(definition, Declaring(), ValidationStage.Save));
    }

    [Theory]
    [InlineData(20)]
    [InlineData(5)]
    [Trait("criterion", "AC-code-actions-10")]
    public void A_copy_whose_parameters_differ_is_a_version_mistake_naming_the_difference_and_no_save_mistake(int length)
    {
        var definition = Holding(Lookup.ToAction());
        var declaration = Declaring(Lookup with { Parameters = [Pesel with { MaxLength = length }] });

        var mistake = Assert.Single(
            DefinitionValidator.Validate(definition, ValidationStage.Version, declaration),
            found => found.Code == MistakeCodes.ActionCodeContract
        );

        Assert.Contains("parameter 'pesel' is text up to 11 here", mistake.Message, StringComparison.Ordinal);
        Assert.Empty(Codes(definition, declaration, ValidationStage.Save));
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-10")]
    public void A_copy_whose_results_differ_is_a_version_mistake()
    {
        var declaration = Declaring(Lookup with { Results = [Pesel with { Name = "policy" }] });

        var mistake = Assert.Single(
            DefinitionValidator.Validate(Holding(Lookup.ToAction()), ValidationStage.Version, declaration),
            found => found.Code == MistakeCodes.ActionCodeContract
        );

        Assert.Contains("result 'policy' is declared and not here", mistake.Message, StringComparison.Ordinal);
        Assert.Empty(Codes(Holding(Lookup.ToAction()), declaration, ValidationStage.Save));
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-10")]
    public void An_http_action_sharing_an_id_with_a_declared_code_action_is_a_version_mistake_and_no_save_mistake()
    {
        var definition = Holding(Http("lookupPolicy"));

        Assert.Equal([MistakeCodes.ActionCodeConflict], Codes(definition, Declaring(Lookup), ValidationStage.Version));
        Assert.Empty(Codes(definition, Declaring(Lookup), ValidationStage.Save));
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-11")]
    public void A_copy_differing_only_in_its_description_and_a_declared_action_not_in_the_definition_give_no_mistake()
    {
        var reworded = Lookup with { Description = "Reworded", Parameters = [Pesel with { Description = "Reworded too" }] };

        Assert.Empty(Codes(Holding(Lookup.ToAction()), Declaring(reworded), ValidationStage.Version));
        Assert.Empty(Codes(Holding(), Declaring(Lookup), ValidationStage.Version));
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-13")]
    public void A_definition_holding_a_code_action_passes_the_validator_offline()
    {
        const string Json =
            """{"formatVersion":4,"actions":[{"id":"lookupPolicy","description":"Finds","parameters":{"pesel":{"type":"text","maxLength":11,"description":"d"}},"implementation":"code"}]}""";

        Assert.Empty(DefinitionValidator.ValidateJson(Json, ValidationStage.Save));
        Assert.DoesNotContain(
            DefinitionValidator.ValidateJson(Json, ValidationStage.Version),
            mistake => mistake.Resource.Kind == ResourceKind.Action
        );
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-6")]
    public void A_declaration_the_cms_could_not_keep_is_invalid_so_the_last_good_one_stands()
    {
        var bad = Declaring(
            new CodeAction
            {
                Id = "Bad Id",
                Description = "d",
                Parameters = [Pesel, Pesel, Pesel with { Name = "a.b" }],
            }
        );

        var codes = DeclarationValidator.CheckAnswer(bad, "b").Select(mistake => mistake.Code).ToList();

        Assert.Contains(MistakeCodes.CodeActionIdInvalid, codes);
        Assert.Contains(MistakeCodes.CodeActionFieldRepeated, codes);
        Assert.Contains(MistakeCodes.CodeActionFieldInvalid, codes);
    }
}
