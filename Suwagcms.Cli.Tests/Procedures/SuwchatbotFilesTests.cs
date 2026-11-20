using System.Text.Json.Nodes;
using Suwagcms.Cli.Procedures;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Cli.Tests.Procedures;

public sealed class SuwchatbotFilesTests
{
    private const int FormatVersion = 5;

    private static readonly System.Text.Json.JsonSerializerOptions Wire = new(System.Text.Json.JsonSerializerDefaults.Web);

    private const string Conversation = """
        id: conversation
        description: The base of every conversation; waits for the next message.
        firstStep: wait
        steps:
          wait:
            listen: {}
            next: wait
        """;

    private const string ValuationReport = """
        id: valuationReport
        description: The customer orders a valuation report for a property; collects what the appraiser needs to prepare it.
        localFacts:
          reportDelivery:
            type: enum
            values: [email, phone]
            description: How the report should reach the customer, by e-mail or by phone.
        firstStep: askDetails
        steps:
          askDetails:
            collect: [customer.firstName, customer.lastName, property.landRegisterNumber]
            next: askDelivery
          askDelivery:
            collect: [reportDelivery]
            next:
              - when: { reportDelivery: { oneOf: [phone] } }
                to: askPhone
              - to: askEmail
          askEmail:
            collect: [customer.email]
            next: tellEmailDelivery
          tellEmailDelivery:
            inform:
              instruction: Thank the customer and tell them what happens next with their valuation report.
              points:
                turnaround: The report is ready within 7 working days of the inspection.
                delivery: The report is sent by e-mail as a PDF.
          askPhone:
            collect: [customer.phone]
            next: tellPhoneDelivery
          tellPhoneDelivery:
            inform:
              instruction: Thank the customer and tell them what happens next with their valuation report.
              points:
                turnaround: The report is ready within 7 working days of the inspection.
                phoneCall: The appraiser calls when the report is ready.
        whenStalled:
          instruction: Say that the report cannot be prepared without these details, and that the customer can come back to it at any time.
        whenInterrupted: discard
        """;

    private const string QuoteRequest = """
        id: quoteRequest
        description: The customer wants to know what a valuation would cost, before ordering; collects what the appraiser needs to send a quote.
        firstStep: askProperty
        steps:
          askProperty:
            collect: [property.type, valuation.purpose, property.area]
            next:
              - when: { property.type: { oneOf: [land] } }
                to: askContact
              - to: askAge
          askAge:
            collect: [property.age]
            next:
              - when: { property.age: { range: { min: 50 } } }
                to: askRenovation
              - to: askContact
          askRenovation:
            collect: [property.lastRenovationYear]
            next: tellConditionReview
          tellConditionReview:
            inform:
              instruction: Tell the customer how the age of the building affects this task. Name no price.
              points:
                conditionReview: The quote follows a review of the building's condition.
            next: askContact
          askContact:
            collect: [customer.firstName, customer.lastName, customer.email]
            next: whatHappensNext
          whatHappensNext:
            inform:
              instruction: Thank the customer and tell them what happens next with their quote. Name no price.
              points:
                quoteTime: The appraiser sends the quote within 2 working days.
                quoteDelivery: The quote is sent by e-mail.
        whenStalled:
          instruction: Say that the appraiser cannot prepare a quote without these details, and that the customer can come back to it at any time.
        whenInterrupted: discard
        """;

    private const string InspectionVisit = """
        id: inspectionVisit
        description: The customer wants to arrange the appraiser's visit to see the property; collects what the appraiser needs to confirm a time.
        firstStep: askDetails
        steps:
          askDetails:
            collect: [customer.firstName, customer.lastName, customer.email, property.address, inspection.preferredTime]
            next: whatHappensNext
          whatHappensNext:
            inform:
              instruction: Thank the customer and tell them what happens next with the visit. Do not confirm any time yourself.
              points:
                confirmation: The appraiser confirms the time of the visit by e-mail within 1 working day.
                documents: Have the land register number and any floor plans ready for the visit.
        whenStalled:
          instruction: Say that the visit cannot be arranged without these details, and that the customer can come back to it at any time.
        whenInterrupted: discard
        """;

    private const string WithKnowledge = """
        id: telling
        description: Tells what happens next.
        firstStep: tell
        steps:
          tell:
            inform:
              instruction: Tell it.
              knowledge:
                - knowledgeBase: faq
                  articles: [deliveryTimes]
                  searchQueries: [how a report is sent]
              points:
                delivery: The report is sent by e-mail.
            next: bye
          bye:
            say: Goodbye.
        """;

    private const string Facts = """
        [
          {"name":"customer.firstName","type":"text","maxLength":100,"writableBy":"correct","description":"The customer's first name."},
          {"name":"customer.lastName","type":"text","maxLength":100,"writableBy":"correct","description":"The customer's last name."},
          {"name":"customer.email","type":"text","maxLength":254,"writableBy":"correct","description":"The customer's e-mail address.",
           "pattern":{"regex":"[^@\\s]+@[^@\\s]+\\.[^@\\s]+","means":"a name, the @ sign and a domain with a dot","example":"anna@example.com","tellCustomer":"afterRefusal"}},
          {"name":"customer.phone","type":"text","maxLength":30,"writableBy":"correct","description":"The customer's phone number.",
           "pattern":{"regex":"\\+?[0-9]( *[0-9]){8,14}","means":"9 to 15 digits, which may start with a + and may have spaces between them","example":"+48 600 700 800","tellCustomer":"afterRefusal"}},
          {"name":"property.landRegisterNumber","type":"text","maxLength":30,"writableBy":"correct","description":"The land register number of the property.",
           "pattern":{"regex":"[A-Z0-9]{4}/[0-9]{8}/[0-9]","means":"4 letters or digits, a slash, 8 digits, a slash and 1 digit, as on the land register's first page","example":"WA1M/00012345/6"}},
          {"name":"property.type","type":"enum","values":["flat","house","land","commercial","another"],"writableBy":"correct","description":"What kind of property it is."},
          {"name":"property.area","type":"int","min":1,"max":10000000,"writableBy":"correct","description":"The area of the property, in square metres."},
          {"name":"property.age","type":"int","min":0,"max":1000,"writableBy":"correct","description":"How old the building is, in years."},
          {"name":"property.lastRenovationYear","type":"int","min":1800,"max":2100,"writableBy":"correct","description":"The year of the last renovation of the building."},
          {"name":"property.address","type":"text","maxLength":200,"writableBy":"correct","description":"The address of the property."},
          {"name":"valuation.purpose","type":"enum","values":["sale","mortgage","inheritance","court","another"],"writableBy":"correct","description":"What the valuation is for."},
          {"name":"inspection.preferredTime","type":"text","maxLength":200,"writableBy":"correct","description":"When the customer would like the appraiser to come, in their own words."}
        ]
        """;

    private const string Persona = """
        {
          "personality": "You are the customer service of a property appraiser. You talk with the appraiser's customers.\nAnswer in the language the customer writes in.",
          "scope": {
            "inScope": "Property valuations by this appraiser: what a valuation needs, how it goes, and the customer's request.\nStarting the tasks you are given.",
            "outOfScope": "Anything unrelated to property valuations.\nSmalltalk beyond a greeting.",
            "whenOutOfScope": "Decline in one short sentence, without lecturing, and say what you can help with instead."
          }
        }
        """;

    private const string Articles = """
        [{"id":"deliveryTimes","knowledgeBase":"faq","title":"Delivery","description":"How long delivery takes","content":"c","llmGuide":"g"}]
        """;

    private static string Definition(params string[] files)
    {
        var procedures = new JsonArray();
        foreach (var file in files)
        {
            var (document, problem) = ProcedureFile.Parse(file, "file");
            procedures.Add((JsonNode?)document ?? throw new InvalidOperationException(problem));
        }
        return new JsonObject
        {
            ["formatVersion"] = FormatVersion,
            ["facts"] = JsonNode.Parse(Facts),
            ["knowledgeBases"] = JsonNode.Parse("""[{"id":"faq","title":"Questions","embedding":"default","results":5}]"""),
            ["articles"] = JsonNode.Parse(Articles),
            ["procedures"] = procedures,
            ["persona"] = JsonNode.Parse(Persona),
        }.ToJsonString();
    }

    private static readonly BotDeclaration ShippedDeclaration = new()
    {
        Bot = "appraiser",
        FormatVersion = FormatVersion,
        Blocks = ["listen", "collect", "say", "assign", "inform"],
        Persona = BotDeclaration.PersonaComplete,
        EmbeddingProfiles = ["default"],
    };

    [Fact]
    [Trait("criterion", "AC-chatbot-contract-4")]
    public void The_shipped_definition_passes_at_version_stage_against_what_the_bot_declares()
    {
        var mistakes = DefinitionValidator.ValidateJson(
            Definition(Conversation, ValuationReport, QuoteRequest, InspectionVisit),
            ValidationStage.Version,
            ShippedDeclaration
        );

        Assert.Empty(mistakes);
    }

    [Fact]
    [Trait("criterion", "AC-chatbot-contract-4")]
    public void The_shipped_definition_reads_back_with_everything_the_engine_needs()
    {
        var definition = BotDefinition.Read(Definition(Conversation, ValuationReport, QuoteRequest, InspectionVisit));
        var report = definition.Procedures.Single(procedure => procedure.Id == "valuationReport");
        var quote = definition.Procedures.Single(procedure => procedure.Id == "quoteRequest");

        Assert.True(report.IsDiscardedWhenInterrupted);
        Assert.False(definition.Procedures.Single(procedure => procedure.Id == "conversation").IsDiscardedWhenInterrupted);
        Assert.Equal(["email", "phone"], report.Locals["reportDelivery"].Values);
        Assert.Equal(["phone"], report.Steps["askDelivery"].Next?.Rules?[0].When?["reportDelivery"].OneOf);
        Assert.Equal(50, quote.Steps["askAge"].Next?.Rules?[0].When?["property.age"].Range?.Min);
        Assert.Equal(["quoteTime", "quoteDelivery"], quote.Steps["whatHappensNext"].Inform?.Points?.Keys);
        var phone = definition.Facts.Single(fact => fact.Name == "customer.phone");
        Assert.Equal(TellCustomers.AfterRefusal, phone.Pattern?.TellCustomer);
        Assert.True(FactValues.Accepts(phone, "+48 600 700 800"));
        Assert.False(FactValues.Accepts(phone, "12345678"));
        Assert.Contains("\n", definition.Persona?.Scope?.InScope, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-chatbot-contract-4")]
    public void What_the_shipped_bot_does_not_run_is_refused_for_it()
    {
        var withStart = """
            id: starting
            description: Starts another task.
            firstStep: go
            steps:
              go:
                start: quoteRequest
            """;

        var mistakes = DefinitionValidator.ValidateJson(
            Definition(Conversation, QuoteRequest, withStart),
            ValidationStage.Version,
            ShippedDeclaration
        );

        Assert.Equal([MistakeCodes.DeclarationBlock], mistakes.Select(mistake => mistake.Code));
    }

    [Fact]
    [Trait("criterion", "AC-inform-block-12")]
    public void The_procedures_of_the_first_bot_are_accepted_at_version_stage_and_by_the_blocks_it_declares()
    {
        var declaration = new BotDeclaration
        {
            Bot = "appraiser",
            FormatVersion = FormatVersion,
            Blocks = ["listen", "collect", "say", "assign", "inform"],
            Knowledge = true,
            EmbeddingProfiles = ["default"],
        };

        var mistakes = DefinitionValidator.ValidateJson(
            Definition(Conversation, ValuationReport, QuoteRequest, InspectionVisit, WithKnowledge),
            ValidationStage.Version,
            declaration
        );

        Assert.Empty(mistakes);
    }

    [Fact]
    [Trait("criterion", "AC-inform-block-12")]
    public void The_format_holds_what_the_file_says_in_the_order_it_says_it()
    {
        var definition =
            System.Text.Json.JsonSerializer.Deserialize<BotDefinition>(Definition(Conversation, ValuationReport), Wire)
            ?? throw new InvalidOperationException("no definition");

        var inform = definition.Procedures.Single(procedure => procedure.Id == "valuationReport").Steps["tellEmailDelivery"].Inform;

        Assert.NotNull(inform);
        Assert.Equal(["turnaround", "delivery"], inform.Points?.Keys);
        Assert.Equal("The report is sent by e-mail as a PDF.", inform.Points?["delivery"]);
        Assert.Contains("tell them what happens next", inform.Instruction, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-inform-block-12")]
    public void The_same_file_with_a_point_taken_out_of_its_shape_is_not_accepted()
    {
        var broken = InspectionVisit.Replace("documents:", "the_documents:", StringComparison.Ordinal);

        var mistakes = DefinitionValidator.ValidateJson(Definition(Conversation, broken), ValidationStage.Version);

        Assert.Equal([MistakeCodes.StepInformPointName], mistakes.Select(mistake => mistake.Code));
    }
}
