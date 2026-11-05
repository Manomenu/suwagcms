# Suwagcms.Sdk

The SDK of a .NET bot (.NET 9 or later) built on Suwagcms.

```sh
dotnet add package Suwagcms.Sdk
```

It holds the definition format (`Suwagcms.Sdk.Format`, JSON schema in `definition-format.schema.json`), the validator
(`Suwagcms.Sdk.Validation`) and the bot runtime (`Suwagcms.Sdk.BotRuntime`).

## The bot runtime

```csharp
builder.Services.AddSuwagcmsBot("rzeczoznawca");   // the bot's own id; versions are kept in files under the content root
var app = builder.Build();
app.MapSuwagcmsBot();                              // GET /suwagcms/state, GET /suwagcms/declaration, PUT /suwagcms/versions/{number}, PUT /suwagcms/current, PUT /suwagcms/availability
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
    Blocks = ["listen", "collect", "say", "assign"],       // what your engine runs: there is no default
    CodeActions = [new CodeAction { Id = "lookupPolicy", Description = "Finds a policy" }],
});
```

The CMS then builds and pushes only versions that fit it. Without `declares` the route answers `declaresNothing: true` and nothing
is checked against the bot, so a bot that is not ready to say still works. The answer's shape is in `bot-runtime.schema.json`.
A bot outside .NET serves the same route and validates a definition through the CMS with `POST /api/validate` (`suwagcms validate`).

The bot's code uses `IBotDefinitions`:

- `CurrentNumber`: the number a new conversation starts on (null before the first version); keep it with the conversation.
- `GetAsync(number)`: the definition of that version, also on an instance that was not pushed to.
- `GetArticlesAsync(number)`: every article of that version, ordered by id, each with its base's `Language` and `IsOff` as it stands now, for your own search index or an
  external one. `Content` and `LlmGuide` are as the version holds them, with `cms://` links; the CMS builds no index and is never called.
- `OnCurrentChanged((number, cancellationToken) => …)`: tells your code each time this instance's current version changes, a rollback
  included, and once at start when there is one. Each piece of news runs on its own task, never holding up a push: a failure is logged, and a slow
  listener may overlap the next, so go by the number. Several instances each hear it. A version is current before you hear of it: nothing waits for your search.
- `OnAvailabilityChanged((number, cancellationToken) => …)`: tells your code each time what is off changes, with the message's number, and once at start when a set is held, the same way, so a search can drop an article at once.
- `IsOff(number, kind, id)`: whether a resource is turned off, for a conversation on that version. Off is a live setting outside versions: `GetAsync` still gives the resource, and what off means is your engine's to carry out.
- `ReportInUseAsync(numbers)`: the versions your open conversations use, **across all instances of the bot**. Versions neither
  current nor reported are removed; until the first report nothing is. When a conversation expires is your decision.

**What is off.** The CMS sends `PUT /suwagcms/availability` with `number` and `off`, the whole set: each item has `kind` (`Article`, `Procedure` or `Action`), `id`, and for a former id of a renamed resource `upToVersion`, the last version it counts in. A lower number changes nothing and the same message twice changes nothing, so a late message cannot undo a newer one. The answer is the state, which reports the setting under `availability`; a refusal has the same shape as for a version (422, or 503 when the store failed).

**Several instances.** Pass a store they all reach: `AddSuwagcmsBot(id, store)` with your own `IVersionStore` (a table in the
bot's database, say). The SDK writes versions, the current number, the reported numbers and what is off to it, the other instances read it every five seconds
(`pollInterval`), and `GET /suwagcms/state` answers from it, so every instance says the same. Your store's `WriteAvailabilityAsync` must keep a message only if its number is not lower than the one held, in one step, since the instances have no lock in common, and `ReadStateAsync` must give it back. A change delivered to one instance reaches the others at their next poll, not sooner. The default `FileVersionStore`
is for one instance; `InMemoryVersionStore` is for tests.

**The routes have no key yet.** Anyone who can reach the bot can replace its definition, and the bot id check only stops a
mistyped address. Until the CMS has identity, expose `/suwagcms/*` to a trusted network only.

Without the runtime, the validator alone needs no database and no network:

```csharp
IReadOnlyList<Mistake> mistakes = DefinitionValidator.ValidateJson(json, ValidationStage.Version);
```

Pass a `BotDeclaration` as a third argument and the `Version` stage also reports what that bot cannot run; `Save` never looks at it.

It returns every mistake at once, each with a stable code and a fix; an empty list means the definition passes.
