using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Actions;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation.Rules;

public sealed class ActionRulesTests
{
    private static BotAction Http(
        string url = "https://crm.internal/check",
        string method = "POST",
        Dictionary<string, string>? headers = null,
        int? timeout = null,
        Dictionary<string, ActionField>? parameters = null,
        Dictionary<string, ActionField>? results = null,
        string id = "check",
        string description = "Checks"
    ) =>
        new()
        {
            Id = id,
            Description = description,
            Parameters = parameters,
            Results = results,
            Implementation = new ActionImplementation
            {
                Http = new HttpCall
                {
                    Method = method,
                    Address = url,
                    Headers = headers,
                    TimeoutSeconds = timeout,
                },
            },
        };

    private static IReadOnlyList<Mistake> Check(params BotAction[] actions) =>
        DefinitionValidator.Validate(new BotDefinition { FormatVersion = 2, Actions = actions }, ValidationStage.Save);

    private static string[] Codes(params BotAction[] actions) => [.. Check(actions).Select(mistake => mistake.Code)];

    [Fact]
    [Trait("criterion", "AC-http-actions-2")]
    public void An_action_with_only_an_id_a_description_and_an_implementation_has_no_mistake() => Assert.Empty(Check(Http()));

    [Fact]
    [Trait("criterion", "AC-http-actions-4")]
    public void Blank_descriptions_bad_names_a_bad_type_and_a_missing_implementation_are_reported_at_once_each_with_a_fix()
    {
        var bad = new BotAction
        {
            Id = "Bad Id",
            Description = " ",
            Parameters = new Dictionary<string, ActionField>
            {
                ["Bad Name"] = new()
                {
                    Type = FactTypes.Text,
                    Description = "d",
                    MaxLength = 3,
                },
                ["noDescription"] = new() { Type = FactTypes.Text, MaxLength = 3 },
            },
            Results = new Dictionary<string, ActionField>
            {
                ["noDescription"] = new() { Type = FactTypes.Text, MaxLength = 3 },
                ["badType"] = new() { Type = "blob", Description = "d" },
                ["oneValue"] = new()
                {
                    Type = FactTypes.Enum,
                    Description = "d",
                    Values = ["a"],
                },
                ["noBounds"] = new() { Type = FactTypes.WholeNumber, Description = "d" },
                ["noLength"] = new() { Type = FactTypes.Text, Description = "d" },
            },
        };

        var mistakes = Check(bad);

        foreach (
            var (code, path) in new[]
            {
                (MistakeCodes.ActionIdInvalid, "id"),
                (MistakeCodes.ActionDescriptionMissing, "description"),
                (MistakeCodes.ActionImplementationMissing, "implementation"),
                (MistakeCodes.FactNameInvalid, "parameters.Bad Name"),
                (MistakeCodes.FactDescriptionMissing, "parameters.noDescription.description"),
                (MistakeCodes.FactDescriptionMissing, "results.noDescription.description"),
                (MistakeCodes.FactTypeInvalid, "results.badType.type"),
                (MistakeCodes.FactEnumTooFewValues, "results.oneValue.values"),
                (MistakeCodes.FactIntBoundMissing, "results.noBounds.min"),
                (MistakeCodes.FactTextMaxLengthInvalid, "results.noLength.maxLength"),
            }
        )
        {
            Assert.Contains(mistakes, mistake => mistake.Code == code && mistake.Path == path && mistake.Fix.Length > 0);
        }
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-4")]
    public void An_id_the_actions_already_have_is_a_duplicate() => Assert.Contains(MistakeCodes.DuplicateId, Codes(Http(), Http()));

    [Theory]
    [InlineData("GRAB", "https://crm.internal", MistakeCodes.ActionMethodInvalid)]
    [InlineData("post", "https://crm.internal", MistakeCodes.ActionMethodInvalid)]
    [InlineData("POST", "/relative", MistakeCodes.ActionUrlInvalid)]
    [InlineData("POST", "ftp://crm.internal", MistakeCodes.ActionUrlInvalid)]
    [InlineData("POST", "https://user:pass@crm.internal", MistakeCodes.ActionUrlCredentials)]
    [InlineData("POST", "https://crm.internal/{unknown}", MistakeCodes.ActionUrlParameter)]
    [Trait("criterion", "AC-http-actions-5")]
    public void A_bad_method_or_url_is_refused_with_a_fix(string method, string url, string code)
    {
        var mistake = Assert.Single(Check(Http(url, method)));

        Assert.Equal(code, mistake.Code);
        Assert.NotEmpty(mistake.Fix);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(61, true)]
    [InlineData(1, false)]
    [InlineData(60, false)]
    [Trait("criterion", "AC-http-actions-5")]
    public void A_timeout_outside_1_to_60_is_refused(int seconds, bool refused)
    {
        var mistakes = Check(Http(timeout: seconds)).Where(mistake => mistake.Code == MistakeCodes.ActionTimeoutInvalid).ToList();

        Assert.Equal(refused, mistakes.Count > 0);
        Assert.All(mistakes, mistake => Assert.NotEmpty(mistake.Fix));
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-5")]
    public void A_placeholder_that_is_a_parameter_is_fine()
    {
        var parameters = new Dictionary<string, ActionField>
        {
            ["city"] = new()
            {
                Type = FactTypes.Text,
                Description = "d",
                MaxLength = 3,
            },
        };

        Assert.Empty(Check(Http("https://crm.internal/{city}?token={city}", parameters: parameters)));
    }

    [Theory]
    [InlineData("X-Thing", "{secret:lower}", MistakeCodes.ActionSecretName)]
    [InlineData("X-Thing", "{secret:1BAD}", MistakeCodes.ActionSecretName)]
    [InlineData("Authorization", "Bearer abc", MistakeCodes.ActionSecretLiteral)]
    [InlineData("proxy-authorization", "abc", MistakeCodes.ActionSecretLiteral)]
    [InlineData("Cookie", "a=b", MistakeCodes.ActionSecretLiteral)]
    [InlineData("X-Api-Key", "abc", MistakeCodes.ActionSecretLiteral)]
    [InlineData("X-Token", "abc", MistakeCodes.ActionSecretLiteral)]
    [InlineData("X-Password", "abc", MistakeCodes.ActionSecretLiteral)]
    [InlineData("X-Client-Secret", "abc", MistakeCodes.ActionSecretLiteral)]
    [Trait("criterion", "AC-http-actions-6")]
    public void A_bad_secret_name_and_a_literal_value_in_a_sensitive_header_are_refused_with_the_reference_to_write(
        string header,
        string value,
        string code
    )
    {
        var mistake = Assert.Single(Check(Http(headers: new() { [header] = value })));

        Assert.Equal(code, mistake.Code);
        Assert.Contains("{secret:", mistake.Fix, StringComparison.Ordinal);
        Assert.DoesNotContain(value, mistake.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://crm.internal/{secret:CRM_TOKEN}", MistakeCodes.ActionSecretInUrl)]
    [InlineData("https://crm.internal/x?apiKey=abc123", MistakeCodes.ActionSecretLiteral)]
    [Trait("criterion", "AC-http-actions-6")]
    public void A_secret_in_the_url_and_a_literal_query_value_with_a_sensitive_name_are_refused_with_a_fix(string url, string code)
    {
        var mistake = Assert.Single(Check(Http(url)));

        Assert.Equal(code, mistake.Code);
        Assert.Contains("{secret:", mistake.Fix, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", mistake.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Authorization", "Bearer {secret:CRM_TOKEN}")]
    [InlineData("X-Customer", "plain")]
    [Trait("criterion", "AC-http-actions-6")]
    public void A_reference_and_a_literal_in_an_ordinary_header_pass(string header, string value) =>
        Assert.Empty(Check(Http(headers: new() { [header] = value })));

    [Fact]
    [Trait("criterion", "AC-http-actions-8")]
    public void The_validator_offline_gives_the_codes_and_messages_of_a_bad_action_from_its_json()
    {
        const string Json =
            """{"formatVersion":2,"actions":[{"id":"check","description":"Checks","implementation":{"http":{"method":"POST","url":"https://crm.internal","headers":{"Authorization":"Bearer abc"}}}}]}""";

        var mistake = Assert.Single(DefinitionValidator.ValidateJson(Json, ValidationStage.Save));

        Assert.Equal(MistakeCodes.ActionSecretLiteral, mistake.Code);
        Assert.Equal("implementation.http.headers.Authorization", mistake.Path);
        Assert.Equal("Header 'Authorization' holds a literal value, and the CMS never stores secrets.", mistake.Message);
    }
}
