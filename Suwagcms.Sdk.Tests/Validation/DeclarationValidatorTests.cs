using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Format.Knowledge;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.Procedures.Blocks;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation;

public sealed class DeclarationValidatorTests
{
    private static BotDeclaration Good(params string[] blocks) =>
        new()
        {
            Bot = "rzeczoznawca",
            FormatVersion = 3,
            Blocks = blocks.Length == 0 ? Block.Names : blocks,
        };

    private static BotDefinition Definition =>
        new()
        {
            FormatVersion = 3,
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
            FormatVersion = 3,
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
            FormatVersion = 4,
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
        var declaration = Good() with { FormatVersion = 4 };

        var mistake = Assert.Single(DefinitionValidator.Validate(Definition, ValidationStage.Version, declaration));

        Assert.Equal(MistakeCodes.DeclarationFormatVersion, mistake.Code);
        Assert.Equal(ValidationStage.Version, mistake.Stage);
        Assert.Equal("This definition is format 3 and the bot wants format 4.", mistake.Message);
        Assert.Contains("declare format 3 in the bot", mistake.Fix, StringComparison.Ordinal);
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

    private static BotDefinition WithBases(params (string Id, string Embedding)[] bases) =>
        Definition with
        {
            KnowledgeBases =
            [
                .. bases.Select(item => new KnowledgeBase
                {
                    Id = item.Id,
                    Title = item.Id,
                    Embedding = item.Embedding,
                    Results = 5,
                }),
            ],
            Articles =
            [
                .. bases.Select(item => new Article
                {
                    Id = $"about{item.Id}",
                    KnowledgeBase = item.Id,
                    Title = item.Id,
                    Description = "d",
                    Content = "c",
                    LlmGuide = "g",
                }),
            ],
        };

    [Fact]
    [Trait("criterion", "AC-bot-declaration-20")]
    public void A_base_naming_a_profile_the_bot_does_not_serve_is_a_version_mistake_naming_the_base_and_the_profiles_served()
    {
        var definition = WithBases(("legal", "legalText"), ("offer", "default"), ("rules", "LEGALTEXT"));
        var declaration = Good() with { EmbeddingProfiles = ["default"] };

        var mistakes = DefinitionValidator
            .Validate(definition, ValidationStage.Version, declaration)
            .Where(found => found.Code == MistakeCodes.DeclarationEmbeddingProfile)
            .ToList();

        Assert.Equal(["legal", "rules"], mistakes.Select(found => found.Resource.Id));
        var mistake = mistakes[0];
        Assert.Equal(ResourceKind.KnowledgeBase, mistake.Resource.Kind);
        Assert.Equal("embedding", mistake.Path);
        Assert.Equal(
            "Knowledge base 'legal' names the embedding profile 'legalText', which this bot does not serve. It serves: default.",
            mistake.Message
        );
        Assert.Equal(
            "Type one of the profiles the bot serves on the knowledge base, or set the variables of 'legalText' on the bot and deploy.",
            mistake.Fix
        );
        Assert.DoesNotContain(
            DefinitionValidator.Validate(definition, ValidationStage.Save, declaration),
            found => found.Code == MistakeCodes.DeclarationEmbeddingProfile
        );
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-20")]
    public void Profiles_match_ignoring_case_none_are_served_when_left_out_a_base_without_articles_is_checked_too_and_a_bot_declaring_nothing_is_not()
    {
        var definition = WithBases(("legal", "legalText"));
        static IEnumerable<Mistake> Profiles(BotDefinition definition, BotDeclaration declaration)
        {
            return DefinitionValidator
                .Validate(definition, ValidationStage.Version, declaration)
                .Where(found => found.Code == MistakeCodes.DeclarationEmbeddingProfile);
        }

        Assert.Empty(Profiles(definition, Good() with { EmbeddingProfiles = ["LEGALTEXT"] }));
        Assert.Contains("It serves: none.", Assert.Single(Profiles(definition, Good())).Message, StringComparison.Ordinal);
        Assert.Empty(Profiles(definition, new BotDeclaration { Bot = "rzeczoznawca", DeclaresNothing = true }));
        Assert.Single(Profiles(definition with { Articles = [] }, Good()));
    }

    private static BotDefinition WithNamedArticles(int contentLength, int guideLength, params string[] named)
    {
        var definition = WithBases(("faq", "default"));
        return definition with
        {
            Articles =
            [
                .. definition.Articles,
                new Article
                {
                    Id = "long",
                    KnowledgeBase = "faq",
                    Title = "Long",
                    Description = "d",
                    Content = new string('c', contentLength),
                    LlmGuide = new string('g', guideLength),
                },
                new Article
                {
                    Id = "short",
                    KnowledgeBase = "faq",
                    Title = "Short",
                    Description = "d",
                    Content = "12345",
                    LlmGuide = "12345",
                },
            ],
            Procedures =
            [
                .. definition.Procedures,
                new Procedure
                {
                    Id = "tell",
                    Description = "Tells",
                    FirstStep = "inform",
                    Steps = new Dictionary<string, ProcedureStep>
                    {
                        ["inform"] = new()
                        {
                            Inform = new InformArguments
                            {
                                Instruction = "Tell it.",
                                Points = new Dictionary<string, string?> { ["fee"] = "The fee." },
                                Knowledge = [new KnowledgeEntry { KnowledgeBase = "faq", Articles = named }],
                            },
                        },
                    },
                },
            ],
        };
    }

    private static IEnumerable<Mistake> TooLong(BotDefinition definition, int? limit) =>
        DefinitionValidator
            .Validate(definition, ValidationStage.Version, Good() with { Knowledge = true, NamedArticlesLimit = limit })
            .Where(found => found.Code == MistakeCodes.DeclarationNamedArticlesTooLong);

    [Fact]
    [Trait("criterion", "AC-bot-declaration-21")]
    public void Named_articles_over_the_limit_together_are_a_version_mistake_with_the_fix_to_search()
    {
        var mistake = Assert.Single(TooLong(WithNamedArticles(60, 31, "long", "short", "long"), 100));

        Assert.Equal(new ResourceRef(ResourceKind.Procedure, "tell"), mistake.Resource);
        Assert.Equal("steps.inform.inform.knowledge", mistake.Path);
        Assert.Contains("hold 101 characters", mistake.Message, StringComparison.Ordinal);
        Assert.Contains("at most 100", mistake.Message, StringComparison.Ordinal);
        Assert.Contains("search query", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-21")]
    public void Named_articles_at_the_limit_or_without_one_pass()
    {
        Assert.Empty(TooLong(WithNamedArticles(60, 30, "long", "short"), 100));
        Assert.Empty(TooLong(WithNamedArticles(6000, 6000, "long"), null));
    }
}
