using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Tests.Validation;

internal static class Samples
{
    public static GlobalFact CityFact { get; } =
        new()
        {
            Name = "customer.city",
            Type = FactTypes.Text,
            Description = "The city the customer lives in",
            MaxLength = 80,
        };

    public static BotDefinition Empty { get; } = new() { FormatVersion = 1 };

    public static BotDefinition Valid { get; } =
        new()
        {
            FormatVersion = 1,
            Facts = [CityFact],
            KnowledgeBases = [new KnowledgeBase { Id = "faq" }],
            Articles = [new Article { Id = "pricing" }],
            Actions = [new BotAction { Id = "sendOffer" }],
            Procedures = [new Procedure { Id = "quote" }],
        };

    public static BotDefinition WithDuplicatesInTwoKinds { get; } =
        new()
        {
            FormatVersion = 1,
            Facts = [CityFact, CityFact],
            Procedures = [new Procedure { Id = "quote" }, new Procedure { Id = "other" }, new Procedure { Id = "quote" }],
        };
}
