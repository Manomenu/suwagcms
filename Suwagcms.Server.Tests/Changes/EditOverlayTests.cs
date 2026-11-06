using System.Text.Json;
using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Server.Tests.Changes;

public sealed class EditOverlayTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static string Json(object value) => JsonSerializer.Serialize(value, value.GetType(), Web);

    [Fact]
    [Trait("criterion", "AC-batch-apply-9")]
    public void An_edit_giving_nothing_keeps_every_field_of_a_procedure_through_every_shape_of_its_steps()
    {
        var stored = JsonSerializer.Deserialize<Procedure>(
            """
            {"id":"p","description":"D","firstStep":"a","whenStalled":{"instruction":"Help"},
             "steps":{
               "a":{"listen":{},"next":[{"when":{"floors":{"known":true}},"to":"b"},{"to":"c"}]},
               "b":{"action":"lookup","with":{"q":"floors"},"results":{"price":"cost"},"onFailure":"c","next":"c"},
               "c":{"say":"Hi","next":"d"},
               "d":{"collect":["floors"],"next":"e"},
               "e":{"assign":{"floors":"2"},"next":"f"},
               "f":{"start":"other","next":"g"},
               "g":{"inform":{"text":"T"},"oddBlock":{"kept":1}}
             },
             "localFacts":{"floors":{"type":"int","description":"F","min":1,"max":9}}}
            """,
            Web
        );
        Assert.NotNull(stored);

        var merged = EditOverlay.Over(stored, new SaveProcedure(null, null, null, null));

        Assert.Equal(Json(stored.Steps), Json(merged.Steps ?? throw new InvalidOperationException()));
        Assert.Equal(stored.Description, merged.Description);
        Assert.Equal(stored.FirstStep, merged.FirstStep);
        Assert.Equal(
            Json(stored.WhenStalled ?? throw new InvalidOperationException()),
            Json(merged.WhenStalled ?? throw new InvalidOperationException())
        );
        Assert.Equal(
            Json(stored.LocalFacts ?? throw new InvalidOperationException()),
            Json(merged.LocalFacts ?? throw new InvalidOperationException())
        );
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-9")]
    public void An_edit_giving_nothing_keeps_every_field_of_an_action_with_its_implementation()
    {
        var stored = JsonSerializer.Deserialize<BotAction>(
            """
            {"id":"a","description":"D",
             "parameters":{"q":{"type":"text","description":"Q"}},"results":{"r":{"type":"int","description":"R","min":1}},
             "implementation":{"http":{"method":"GET","url":"https://x.internal/{q}","timeoutSeconds":20}}}
            """,
            Web
        );
        Assert.NotNull(stored);

        var merged = EditOverlay.Over(stored, new SaveAction(null, null, null, null));

        Assert.Equal(Json(new SaveAction(stored.Description, stored.Parameters, stored.Results, stored.Implementation)), Json(merged));
    }

    [Theory]
    [Trait("criterion", "AC-batch-apply-9")]
    [InlineData(null, false, "regex", 5)]
    [InlineData(null, true, null, 5)]
    [InlineData("int", false, null, null)]
    public void A_fact_keeps_its_constraints_unless_its_type_changes_and_loses_its_pattern_when_told(
        string? type,
        bool clearsPattern,
        string? regex,
        int? maxLength
    )
    {
        var stored = new GlobalFact
        {
            Name = "f",
            Type = "text",
            Description = "D",
            WritableBy = FactWriters.Set,
            MaxLength = 5,
            Pattern = new TextPattern
            {
                Regex = "regex",
                Means = "M",
                TellCustomer = TellCustomers.Upfront,
            },
        };

        var merged = EditOverlay.OverFact(stored, new SaveFact(type, null, null, null, null, null, null, null), clearsPattern);

        Assert.Equal(type ?? "text", merged.Type);
        Assert.Equal("D", merged.Description);
        Assert.Equal(regex, merged.Pattern?.Regex);
        Assert.Equal(maxLength, merged.MaxLength);
    }
}
