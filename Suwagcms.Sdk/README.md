# Suwagcms.Sdk

The SDK of a .NET bot (.NET 9 or later) built on Suwagcms.

```sh
dotnet add package Suwagcms.Sdk
```

The package is not published for now: until it is, a bot imports this project by a local path, a temporary arrangement.
Who owns what between the CMS and a bot is settled in the spec `chatbot-contract`.

It holds the definition format (`Suwagcms.Sdk.Format`, JSON schema in `definition-format.schema.json`), the validator
(`Suwagcms.Sdk.Validation`) and the bot runtime (`Suwagcms.Sdk.BotRuntime`). Each has a namespace per concept under it, such as
`Suwagcms.Sdk.Format.Facts` or `Suwagcms.Sdk.BotRuntime.Versions`.

## The bot runtime

```csharp
builder.Services.AddSuwagcmsBot("rzeczoznawca");   // the bot's own id; versions are kept in files under the content root
var app = builder.Build();
app.MapSuwagcmsBot();                              // GET /suwagcms/state, GET /suwagcms/declaration, PUT /suwagcms/versions/{number}, PUT /suwagcms/current, PUT /suwagcms/availability, POST /suwagcms/actions/test
```

The CMS pushes a version with `PUT /suwagcms/versions/{number}`: one JSON object with `formatVersion`, `bot`, `number`,
`digest` (`VersionDigest.Of`) and the lists of the definition. The SDK validates it (`DefinitionValidator` at the `Version`
stage), checks the bot id, the number and the digest, and loads it without a restart. A refusal is Problem Details with every
mistake in `mistakes` (422 invalid, 409 the number holds another document, 503 the store failed) and changes nothing.
Sending the same document again succeeds and changes nothing; a lower number not yet held is accepted and becomes current.
`PUT /suwagcms/current` with `{ "number": 12, "digest": "…" }` makes a version the bot already holds current without receiving it
again, and is how the CMS goes back to one (or forward after a rollback). It checks the held document with today's validator
and answers as above, safe to repeat: 409 `version.notHeld` (the CMS then sends the document), 409 `version.conflict` (another
document under that number), 422 (no longer valid), 503 (the store failed). Without this route a bot can only take versions
it does not hold.
`GET /suwagcms/state` answers `bot`, `current` (null before the first version), `held`, `inUse`, `storeError` and `availability`. The shapes
are in `bot-runtime.schema.json`, so a bot outside .NET can implement the same routes.

**What the bot supports.** State it in the setup, and the CMS asks for it with `GET /suwagcms/declaration` once the bot is running:

```csharp
builder.Services.AddSuwagcmsBot("rzeczoznawca", declares: new BotCapabilities
{
    FormatVersion = BotDefinition.CurrentFormatVersion,
    Blocks = ["listen", "collect", "say", "assign", "action"],       // what your engine runs: there is no default
    CodeActions = [new CodeAction { Id = "lookupPolicy", Description = "Finds a policy" }],
    Persona = BotDeclaration.PersonaRequired,                        // optional: a version without a persona is then refused
});
```

The CMS then builds and pushes only versions that fit it. Without `declares` the route answers `declaresNothing: true` and nothing
is checked against the bot, so a bot that is not ready to say still works. The answer's shape is in `bot-runtime.schema.json`.
A bot outside .NET serves the same route and validates a definition through the CMS with `POST /api/validate` (`suwagcms validate`).

A procedure may hold `whenInterrupted`, `keep` or `discard`: what happens to it when another one is started on top of it. `Procedure.IsDiscardedWhenInterrupted` is false when it is left out; what keeping and discarding do is your engine's to carry out.

A version may hold a `persona`: `personality`, and `scope` with `inScope`, `outOfScope` and `whenOutOfScope`, each one text with its line
breaks. `GetAsync` gives it as `BotDefinition.Persona`, null when the version has none; what the bot does with it is its own concern.
`PersonaRequired` is satisfied by any text; `BotDeclaration.PersonaComplete` only by all four parts.

The bot's code uses `IBotDefinitions`:

- `CurrentNumber`: the number a new conversation starts on (null before the first version); keep it with the conversation.
- `GetAsync(number)`: the definition of that version, also on an instance that was not pushed to.
- `GetArticlesAsync(number)`: every article of that version, ordered by id, each with its base's `Language` and `IsOff` as it stands now, for your own search index or an
  external one. `Content` and `LlmGuide` are as the version holds them, with `cms://` links; the CMS builds no index and is never called.
- `OnCurrentChanged((number, cancellationToken) => …)`: tells your code each time this instance's current version changes, a rollback
  included, and once at start when there is one. Each piece of news runs on its own task, never holding up a push: a failure is logged, and a slow
  listener may overlap the next, so go by the number. Several instances each hear it. A version is current before you hear of it: nothing waits for your search.
- `OnAvailabilityChanged((number, cancellationToken) => …)`: tells your code each time what is off changes, with the message's number, and once at start when a set is held, the same way, so a search can drop an article at once.
- `IsOff(number, kind, id)`: whether a resource is turned off, for a conversation on that version. Off is a live setting outside versions: `GetAsync` still gives the resource, and what off means is your engine's to carry out; a step running a turned-off action has its `OnFailure` step to take.
- `ReportInUseAsync(numbers)`: the versions your open conversations use, **across all instances of the bot**. Versions neither
  current nor reported are removed; until the first report nothing is. A push or a rollback removes the version it replaced unless your last report named it, so report a number before you save the first conversation on it. When a conversation expires is your decision.

**What is off.** The CMS sends `PUT /suwagcms/availability` with `number` and `off`, the whole set: each item has `kind` (`Article`, `Procedure` or `Action`), `id`, and for a former id of a renamed resource `upToVersion`, the last version it counts in. A lower number changes nothing and the same message twice changes nothing, so a late message cannot undo a newer one. The answer is the state, which reports the setting under `availability`; a refusal has the same shape as for a version (422, or 503 when the store failed).

**Several instances.** Pass a store they all reach: `AddSuwagcmsBot(id, store)` with your own `IVersionStore` (a table in the
bot's database, say). The SDK writes versions, the current number, the reported numbers and what is off to it, the other instances read it every five seconds
(`pollInterval`), and `GET /suwagcms/state` answers from it, so every instance says the same. Your store's `WriteAvailabilityAsync` must keep a message only if its number is not lower than the one held, in one step, since the instances have no lock in common, and `ReadStateAsync` must give it back. A change delivered to one instance reaches the others at their next poll, not sooner. The default `FileVersionStore`
is for one instance; `InMemoryVersionStore` is for tests.

**Running an HTTP action.** Actions are defined in the CMS and reach the bot in a version; your engine decides when to run one, and
`HttpActionRunner` makes the call the same way for every bot:

```csharp
using var runner = new HttpActionRunner();
ActionOutcome outcome = await runner.RunAsync(action, new Dictionary<string, string> { ["address"] = "Polna 3, Suwałki" });
```

A `{name}` in the URL is filled in URL-encoded; the other parameters go as a query string for `GET` and `DELETE` and as one JSON object
otherwise. Each result is the member of its name at the top level of the JSON answer; with no results declared any 2xx is a success.
A parameter that does not fit, a secret not found, no connection, no answer within the action's `timeoutSeconds` (10 by default), an
answer outside 2xx (redirects included) or results that do not fit give a failure with a `Reason` and no results, and there are no
retries. A secret written `{secret:CRM_TOKEN}` is read from the variable `SUWAGCMS_SECRET_CRM_TOKEN`, so the bot's owner chooses what a
definition can reach; pass your own `findSecret` to look elsewhere. A secret's value is masked in everything the outcome holds. A bot
outside .NET does the same, by the same contract.

**Trying an action.** `POST /suwagcms/actions/test` takes `{ action, values }`, runs the action with the same runner, writes nothing,
and answers `outcome` (`success` or `failure`), `results`, `reason`, `status`, `elapsedMs`, `answerStart` and `answerCut`; the shapes
are in `bot-runtime.schema.json`. A code action runs through the `runCodeAction` you pass to `AddSuwagcmsBot`; without one the answer says so.

**The routes have no key yet.** Anyone who can reach the bot can replace its definition or make it call any address with its secrets, and the bot id check only stops a
mistyped address. Until the CMS has identity, expose `/suwagcms/*` to a trusted network only.

Without the runtime, the validator alone needs no database and no network:

```csharp
IReadOnlyList<Mistake> mistakes = DefinitionValidator.ValidateJson(json, ValidationStage.Version);
```

A bot that handles definitions itself reads one with `BotDefinition.Read(json)`, which uses the format's own options, and tests a value of
a fact with `FactValues.Accepts(fact, value)` and words what it takes with `FactValues.Valid(fact)`, so it keeps no second copy of a pattern's rules.

Pass a `BotDeclaration` as a third argument and the `Version` stage also reports what that bot cannot run; `Save` never looks at it.

It returns every mistake at once, each with a stable code and a fix; an empty list means the definition passes.
