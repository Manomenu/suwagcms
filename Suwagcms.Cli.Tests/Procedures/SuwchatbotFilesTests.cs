using System.Text.Json.Nodes;
using Suwagcms.Cli.Procedures;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Cli.Tests.Procedures;

// Why: this repository cannot read Suwchatbot's, so these are copies of its AppraiserBot/BotFiles/procedures at master 5579b3c; the facts are its facts.yaml without pattern, which this format does not hold yet.
public sealed class SuwchatbotFilesTests
{
    private const int FormatVersion = 1;

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
        firstStep: askDetails
        steps:
          askDetails:
            collect: [customer.firstName, customer.lastName, customer.email, property.landRegisterNumber]
            next: whatHappensNext
          whatHappensNext:
            inform:
              instruction: Thank the customer and tell them what happens next with their valuation report.
              points:
                turnaround: The report is ready within 7 working days of the inspection.
                delivery: The report is sent by e-mail as a PDF.
        whenStalled:
          instruction: Say that the report cannot be prepared without these details, and that the customer can come back to it at any time.
        """;

    private const string QuoteRequest = """
        id: quoteRequest
        description: The customer wants to know what a valuation would cost, before ordering; collects what the appraiser needs to send a quote.
        firstStep: askDetails
        steps:
          askDetails:
            collect: [property.type, valuation.purpose, property.area, customer.firstName, customer.lastName, customer.email]
            next: whatHappensNext
          whatHappensNext:
            inform:
              instruction: Thank the customer and tell them what happens next with their quote. Name no price.
              points:
                quoteTime: The appraiser sends the quote within 2 working days.
                quoteDelivery: The quote is sent by e-mail.
        whenStalled:
          instruction: Say that the appraiser cannot prepare a quote without these details, and that the customer can come back to it at any time.
        """;

    private const string WithContext = """
        id: telling
        description: Tells what happens next.
        firstStep: tell
        steps:
          tell:
            inform:
              instruction: Tell it.
              context:
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
          {"name":"customer.email","type":"text","maxLength":254,"writableBy":"correct","description":"The customer's e-mail address."},
          {"name":"property.landRegisterNumber","type":"text","maxLength":30,"writableBy":"correct","description":"The land register number."},
          {"name":"property.type","type":"enum","values":["flat","house","land","commercial","otherType"],"writableBy":"correct","description":"What kind of property it is."},
          {"name":"property.area","type":"int","min":1,"max":10000000,"writableBy":"correct","description":"The area, in square metres."},
          {"name":"valuation.purpose","type":"enum","values":["sale","mortgage","inheritance","court","otherPurpose"],"writableBy":"correct","description":"What the valuation is for."}
        ]
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
            ["knowledgeBases"] = JsonNode.Parse("""[{"id":"faq","title":"Questions"}]"""),
            ["articles"] = JsonNode.Parse(Articles),
            ["procedures"] = procedures,
        }.ToJsonString();
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
        };

        var mistakes = DefinitionValidator.ValidateJson(
            Definition(Conversation, ValuationReport, QuoteRequest, WithContext),
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

        var inform = definition.Procedures.Single(procedure => procedure.Id == "valuationReport").Steps["whatHappensNext"].Inform;

        Assert.NotNull(inform);
        Assert.Equal(["turnaround", "delivery"], inform.Points?.Keys);
        Assert.Equal("The report is sent by e-mail as a PDF.", inform.Points?["delivery"]);
        Assert.Contains("tell them what happens next", inform.Instruction, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-inform-block-12")]
    public void The_same_file_with_a_point_taken_out_of_its_shape_is_not_accepted()
    {
        var broken = ValuationReport.Replace("turnaround:", "turn_around:", StringComparison.Ordinal);

        var mistakes = DefinitionValidator.ValidateJson(Definition(Conversation, broken), ValidationStage.Version);

        Assert.Equal([MistakeCodes.StepInformPointName], mistakes.Select(mistake => mistake.Code));
    }
}
