using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Suwagcms.Contracts;
using Suwagcms.Sdk.BotRuntime;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Availability;
using Suwagcms.Server.Bots;
using Suwagcms.Server.Database;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Actions;

// Why: the bot makes the call, never the CMS, because only it has the secrets; this reads what it needs, lets go of the database and only then waits, so no save waits on a slow service, and it records nothing, because the answer is the whole value of a run.
internal sealed class ActionTester(Pool pool, BotLine line, IOptions<PushOptions> options)
{
    public const string ValueUnfit = "action.test.value";
    public const string Unreachable = "unreachable";
    public const string NotOffered = "notOffered";
    public const string OffNote = "This action is turned off: procedures will not run it until it is turned on.";

    // Why: a code action has no timeout of its own, and the CMS still has to stop waiting.
    private const int CodeSeconds = 60;

    public async Task<(ActionTestAnswer? Answer, EditResult? Refusal)> RunAsync(
        string bot,
        string id,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken
    )
    {
        var target = new ResourceRef(ResourceKind.Action, id);
        if (await ResourceStore.ReadAsync(pool, bot, cancellationToken) is not { } stored)
        {
            return (null, new EditResult.UnknownBot());
        }
        if (stored.SingleOrDefault(item => item.Target == target)?.Record is not BotAction action)
        {
            return (null, new EditResult.Missing(target));
        }
        var unfit = ActionValues.Check(action, values);
        if (unfit.Count > 0)
        {
            return (
                null,
                new EditResult.Refused([
                    .. unfit.Select(item => new Mistake(
                        ValueUnfit,
                        ValidationStage.Save,
                        target,
                        $"values.{item.Parameter}",
                        item.Message,
                        item.Fix,
                        []
                    )),
                ])
            );
        }
        var registered = await DeclarationStore.FindAsync(pool, bot, cancellationToken);
        var address = registered?.Address ?? throw new InvalidOperationException("the bot was found a moment ago");
        var seconds =
            (action.Implementation?.Http is { } http ? http.TimeoutSeconds ?? HttpCall.DefaultTimeoutSeconds : CodeSeconds)
            + options.Value.ActionTestMarginSeconds;
        var request = new JsonObject
        {
            ["action"] = JsonNode.Parse(ResourceKinds.Write(action)),
            ["values"] = new JsonObject(values.Select(value => KeyValuePair.Create<string, JsonNode?>(value.Key, value.Value))),
        };
        var answer = Judged(bot, address, await line.TestActionAsync(address, request.ToJsonString(), seconds, cancellationToken));
        var off = (await AvailabilityStore.ListAsync(pool, bot, [ResourceKind.Action], cancellationToken)).Any(item =>
            item.Id == id && item.Off
        );
        return (off ? answer with { Note = OffNote } : answer, null);
    }

    private static ActionTestAnswer Judged(string bot, string address, BotLine.Reply reply) =>
        reply switch
        {
            BotLine.Reply.Silent silent => Unjudged(
                Unreachable,
                $"The bot could not be asked: {silent.Reason}. The action has not been judged. Check that bot {bot} is running and that the CMS can reach it."
            ),
            BotLine.Reply.Answer { Status: StatusCodes.Status404NotFound } => Unjudged(
                NotOffered,
                $"Bot {bot} does not offer test runs. Update its Suwagcms.Sdk and deploy; the action has not been judged."
            ),
            BotLine.Reply.Answer answer when BotLine.OutcomeIn(answer) is { } outcome => new ActionTestAnswer(
                outcome.Outcome,
                outcome.Results,
                outcome.Reason,
                outcome.Status,
                outcome.ElapsedMs,
                outcome.AnswerStart,
                outcome.AnswerCut
            ),
            BotLine.Reply.Answer answer => Unjudged(
                Unreachable,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{address} answered {answer.Status}, which is not the answer of a bot's test route. The action has not been judged."
                )
            ),
            _ => throw new InvalidOperationException("a bot's reply is an answer or silence"),
        };

    private static ActionTestAnswer Unjudged(string outcome, string reason) =>
        new(outcome, new Dictionary<string, string>(), reason, null, 0, null, false);
}
