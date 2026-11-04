using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Tests.Validation;

internal static class Samples
{
    public static BotDefinition Empty { get; } = new() { FormatVersion = 1 };

    public static BotDefinition Valid { get; } =
        new()
        {
            FormatVersion = 1,
            Facts = [new GlobalFact { Name = "customer.city" }],
            KnowledgeBases = [new KnowledgeBase { Id = "faq" }],
            Articles = [new Article { Id = "pricing" }],
            Actions = [new BotAction { Id = "sendOffer" }],
            Procedures = [new Procedure { Id = "quote" }],
        };

    public static BotDefinition WithDuplicatesInTwoKinds { get; } =
        new()
        {
            FormatVersion = 1,
            Facts = [new GlobalFact { Name = "customer.city" }, new GlobalFact { Name = "customer.city" }],
            Procedures = [new Procedure { Id = "quote" }, new Procedure { Id = "other" }, new Procedure { Id = "quote" }],
        };
}
