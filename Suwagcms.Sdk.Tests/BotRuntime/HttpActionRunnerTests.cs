using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Suwagcms.Sdk.BotRuntime;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Tests.BotRuntime;

public sealed class HttpActionRunnerTests
{
    private const string Token = "s3cr3t-value";

    private static readonly ActionField Address = new()
    {
        Type = FactTypes.Text,
        Description = "The address",
        MaxLength = 200,
    };

    private static readonly ActionField Floors = new()
    {
        Type = FactTypes.WholeNumber,
        Description = "Floors",
        Min = 1,
        Max = 40,
    };

    private static readonly ActionField Served = new()
    {
        Type = FactTypes.Enum,
        Description = "Whether we serve it",
        Values = ["yes", "no"],
    };

    private static BotAction Action(
        string url,
        string method = "POST",
        Dictionary<string, ActionField>? parameters = null,
        Dictionary<string, ActionField>? results = null,
        Dictionary<string, string>? headers = null,
        int? timeout = null
    ) =>
        new()
        {
            Id = "checkServiceability",
            Description = "Checks",
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

    private static HttpActionRunner Runner(string? secret = Token, TimeProvider? time = null) =>
        new(name => name == "CRM_TOKEN" ? secret : null, time);

    private static Dictionary<string, string> Values(params (string Name, string Value)[] values) =>
        values.ToDictionary(value => value.Name, value => value.Value);

    private static CancellationToken Now => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    [Trait("criterion", "AC-http-actions-13")]
    public async Task Get_and_delete_send_the_other_parameters_as_a_query_string_and_fill_the_url_encoded(string method)
    {
        await using var service = await TestService.StartAsync();
        using var runner = Runner();
        var action = Action(
            service.Address + "/cities/{city}/check",
            method,
            new() { ["city"] = Address, ["address"] = Address },
            headers: new() { ["X-Source"] = "cms" }
        );

        var outcome = await runner.RunAsync(action, Values(("city", "Nowy Targ/1"), ("address", "Polna 3, Suwałki")), Now);

        Assert.True(outcome.Succeeded, outcome.Reason);
        var request = Assert.Single(service.Requests);
        Assert.Equal(method, request.Method);
        Assert.Equal("/cities/Nowy%20Targ%2F1/check?address=Polna%203%2C%20Suwa%C5%82ki", request.PathAndQuery);
        Assert.Equal("", request.Body);
        Assert.Equal("cms", request.Headers["X-Source"]);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [Trait("criterion", "AC-http-actions-13")]
    public async Task Other_methods_send_the_other_parameters_as_one_json_object_with_numbers_as_numbers(string method)
    {
        await using var service = await TestService.StartAsync();
        using var runner = Runner();
        var action = Action(
            service.Address + "/c/{city}",
            method,
            new()
            {
                ["city"] = Address,
                ["address"] = Address,
                ["floors"] = Floors,
            }
        );

        await runner.RunAsync(action, Values(("city", "X"), ("address", "Polna 3"), ("floors", "2")), Now);

        var request = Assert.Single(service.Requests);
        Assert.Equal(method, request.Method);
        Assert.Equal("/c/X", request.PathAndQuery);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("Polna 3", body.RootElement.GetProperty("address").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("floors").GetInt32());
        Assert.False(body.RootElement.TryGetProperty("city", out _));
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-14")]
    public async Task A_secret_reference_reaches_the_service_as_the_value_found_for_that_name()
    {
        await using var service = await TestService.StartAsync();
        using var runner = Runner();

        await runner.RunAsync(Action(service.Address, headers: new() { ["Authorization"] = "Bearer {secret:CRM_TOKEN}" }), Values(), Now);

        Assert.Equal($"Bearer {Token}", Assert.Single(service.Requests).Headers["Authorization"]);
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-14")]
    public void Without_a_finder_a_secret_is_read_only_from_a_variable_with_the_prefix()
    {
        Environment.SetEnvironmentVariable("SUWAGCMS_SECRET_ONE_TEST", "found");
        Environment.SetEnvironmentVariable("ONE_TEST", "reachable");

        Assert.Equal("found", ActionSecrets.FromEnvironment("ONE_TEST"));
        Assert.Null(ActionSecrets.FromEnvironment("PATH"));
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-15")]
    public async Task A_2xx_answer_returns_each_declared_result_by_name()
    {
        await using var service = await TestService.StartAsync();
        service.Answer = context => context.Response.WriteAsync("""{ "serviceable": "yes", "floors": 3, "other": true }""");
        using var runner = Runner();

        var outcome = await runner.RunAsync(
            Action(service.Address, results: new() { ["serviceable"] = Served, ["floors"] = Floors }),
            Values(),
            Now
        );

        Assert.True(outcome.Succeeded, outcome.Reason);
        Assert.Equal(200, outcome.Status);
        Assert.Equal(Values(("serviceable", "yes"), ("floors", "3")), outcome.Results);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("")]
    [Trait("criterion", "AC-http-actions-15")]
    public async Task With_no_results_declared_any_2xx_answer_is_a_success_whatever_its_body(string body)
    {
        await using var service = await TestService.StartAsync();
        service.Answer = context =>
        {
            context.Response.StatusCode = 204 + (body.Length == 0 ? 0 : -3);
            return context.Response.WriteAsync(body);
        };
        using var runner = Runner();

        var outcome = await runner.RunAsync(Action(service.Address), Values(), Now);

        Assert.True(outcome.Succeeded, outcome.Reason);
        Assert.Empty(outcome.Results);
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-16")]
    public async Task A_parameter_missing_or_not_fitting_says_which_and_what_fits_and_sends_nothing()
    {
        await using var service = await TestService.StartAsync();
        using var runner = Runner();
        var action = Action(service.Address, parameters: new() { ["address"] = Address, ["floors"] = Floors });

        var outcome = await runner.RunAsync(action, Values(("floors", "99")), Now);

        Assert.False(outcome.Succeeded);
        Assert.Contains("'address'", outcome.Reason, StringComparison.Ordinal);
        Assert.Contains("'floors'", outcome.Reason, StringComparison.Ordinal);
        Assert.Contains("from 1 to 40", outcome.Reason, StringComparison.Ordinal);
        Assert.Contains("Nothing was sent", outcome.Reason, StringComparison.Ordinal);
        Assert.Empty(outcome.Results);
        Assert.Empty(service.Requests);
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-16")]
    public async Task A_secret_that_cannot_be_found_is_named_and_nothing_is_sent()
    {
        await using var service = await TestService.StartAsync();
        using var runner = Runner(secret: null);

        var outcome = await runner.RunAsync(Action(service.Address, headers: new() { ["X-Token"] = "{secret:CRM_TOKEN}" }), Values(), Now);

        Assert.False(outcome.Succeeded);
        Assert.Contains("CRM_TOKEN", outcome.Reason, StringComparison.Ordinal);
        Assert.Empty(service.Requests);
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-16")]
    public async Task No_connection_says_so_with_no_results()
    {
        await using var service = await TestService.StartAsync();
        var address = service.Address;
        await service.DisposeAsync();
        using var runner = Runner();

        var outcome = await runner.RunAsync(Action(address, results: new() { ["serviceable"] = Served }), Values(), Now);

        Assert.False(outcome.Succeeded);
        Assert.Contains("No connection", outcome.Reason, StringComparison.Ordinal);
        Assert.Empty(outcome.Results);
    }

    [Theory]
    [InlineData(503)]
    [InlineData(302)]
    [InlineData(404)]
    [Trait("criterion", "AC-http-actions-16")]
    public async Task An_answer_outside_2xx_redirects_included_says_the_status_and_sends_one_request(int status)
    {
        await using var service = await TestService.StartAsync();
        service.Answer = context =>
        {
            context.Response.StatusCode = status;
            context.Response.Headers.Location = service.Address + "/elsewhere";
            return Task.CompletedTask;
        };
        using var runner = Runner();

        var outcome = await runner.RunAsync(Action(service.Address), Values(), Now);

        Assert.False(outcome.Succeeded);
        Assert.Equal(status, outcome.Status);
        Assert.Contains(status.ToString(System.Globalization.CultureInfo.InvariantCulture), outcome.Reason, StringComparison.Ordinal);
        Assert.Empty(outcome.Results);
        Assert.Single(service.Requests);
    }

    [Theory]
    [InlineData("[1]", "not a JSON object")]
    [InlineData("<html>", "not a JSON object")]
    [InlineData("""{ "status": "ok" }""", "result 'serviceable' is missing")]
    [InlineData("""{ "serviceable": "maybe" }""", "Use one of: yes, no")]
    [InlineData("""{ "serviceable": 1 }""", "does not fit")]
    [Trait("criterion", "AC-http-actions-16")]
    public async Task An_answer_that_does_not_give_the_results_says_which_and_what_fits_with_no_results(string body, string says)
    {
        await using var service = await TestService.StartAsync();
        service.Answer = context => context.Response.WriteAsync(body);
        using var runner = Runner();
        var action = Action(service.Address, results: new() { ["serviceable"] = Served, ["floors"] = Floors with { Min = 0 } });

        var outcome = await runner.RunAsync(action, Values(), Now);

        Assert.False(outcome.Succeeded);
        Assert.Contains(says, outcome.Reason, StringComparison.Ordinal);
        Assert.Empty(outcome.Results);
        Assert.Single(service.Requests);
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-17")]
    public async Task The_actions_own_timeout_ends_a_call_as_a_failure_saying_so()
    {
        await using var service = await TestService.StartAsync();
        service.Answer = context => Task.Delay(TimeSpan.FromSeconds(30), context.RequestAborted);
        using var runner = Runner();

        var outcome = await runner.RunAsync(Action(service.Address, timeout: 1), Values(), Now);

        Assert.False(outcome.Succeeded);
        Assert.Contains("within the time limit of 1 seconds", outcome.Reason, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-17")]
    public async Task Without_a_timeout_a_call_ends_after_ten_seconds()
    {
        await using var service = await TestService.StartAsync();
        service.Answer = context => Task.Delay(TimeSpan.FromSeconds(30), context.RequestAborted);
        using var runner = Runner(time: new HundredTimesFaster());

        var outcome = await runner.RunAsync(Action(service.Address), Values(), Now);

        Assert.False(outcome.Succeeded);
        Assert.Contains("within the time limit of 10 seconds", outcome.Reason, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-http-actions-18")]
    public async Task No_reason_holds_a_secrets_value_even_when_the_service_repeats_it()
    {
        await using var service = await TestService.StartAsync();
        service.Answer = context =>
        {
            context.Response.StatusCode = 500;
            return context.Response.WriteAsync($"token was {Token}");
        };
        using var runner = Runner();

        var outcome = await runner.RunAsync(
            Action(service.Address, headers: new() { ["Authorization"] = "Bearer {secret:CRM_TOKEN}" }),
            Values(),
            Now
        );

        Assert.False(outcome.Succeeded);
        Assert.DoesNotContain(Token, outcome.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(Token, outcome.AnswerStart, StringComparison.Ordinal);
        Assert.Contains("***", outcome.AnswerStart, StringComparison.Ordinal);
    }

    private sealed class HundredTimesFaster : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            System.CreateTimer(callback, state, dueTime / 100, period);
    }
}
