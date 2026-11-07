using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Actions;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.Knowledge;
using Suwagcms.Sdk.Format.Procedures;

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

    public static KnowledgeBase Faq { get; } =
        new()
        {
            Id = "faq",
            Title = "Questions",
            Embedding = "default",
            Results = 5,
        };

    public static Article Pricing { get; } =
        new()
        {
            Id = "pricing",
            KnowledgeBase = "faq",
            Title = "Price list",
            Content = "What a valuation costs.",
            LlmGuide = "Ask which kind of property first.",
            Description = "What a valuation costs and what affects the price",
        };

    public static Procedure Procedure(string id) =>
        new()
        {
            Id = id,
            Description = "A procedure for the tests",
            FirstStep = "hello",
            Steps = new Dictionary<string, ProcedureStep> { ["hello"] = new() { Say = "Hello" } },
        };

    public static BotDefinition Empty { get; } = new() { FormatVersion = 3 };

    public static BotDefinition Valid { get; } =
        new()
        {
            FormatVersion = 3,
            Facts = [CityFact],
            KnowledgeBases = [Faq],
            Articles = [Pricing],
            Actions =
            [
                new BotAction
                {
                    Id = "sendOffer",
                    Description = "Sends the offer",
                    Implementation = new ActionImplementation
                    {
                        Http = new HttpCall { Method = "POST", Address = "https://crm.internal/offers" },
                    },
                },
            ],
            Procedures = [Procedure("conversation"), Procedure("quote")],
        };

    public static BotDefinition WithDuplicatesInTwoKinds { get; } =
        new()
        {
            FormatVersion = 3,
            Facts = [CityFact, CityFact],
            Procedures = [Procedure("conversation"), Procedure("quote"), Procedure("other"), Procedure("quote")],
        };
}
