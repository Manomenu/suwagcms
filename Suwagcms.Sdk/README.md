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
app.MapSuwagcmsBot();                              // GET /suwagcms/state, GET /suwagcms/declaration, PUT /suwagcms/versions/{number}
```

The CMS pushes a version with `PUT /suwagcms/versions/{number}`: one JSON object with `formatVersion`, `bot`, `number`,
`digest` (`VersionDigest.Of`) and the lists of the definition. The SDK validates it (`DefinitionValidator` at the `Version`
stage), checks the bot id, the number and the digest, and loads it without a restart. A refusal is Problem Details with every
mistake in `mistakes` (422 invalid, 409 the number holds another document, 503 the store failed) and changes nothing.
Sending the same document again succeeds and changes nothing; a lower number is accepted and becomes current.
`GET /suwagcms/state` answers `bot`, `current` (null before the first version), `held`, `inUse` and `storeError`. The shapes
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
- `ReportInUseAsync(numbers)`: the versions your open conversations use, **across all instances of the bot**. Versions neither
  current nor reported are removed; until the first report nothing is. When a conversation expires is your decision.

**Several instances.** Pass a store they all reach: `AddSuwagcmsBot(id, store)` with your own `IVersionStore` (a table in the
bot's database, say). The SDK writes versions and the reported numbers to it, the other instances read it every five seconds
(`pollInterval`), and `GET /suwagcms/state` answers from it, so every instance says the same. The default `FileVersionStore`
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

**The routes have no key yet.** Anyone who can reach the bot can replace its definition, and the bot id check only stops a
mistyped address. Until the CMS has identity, expose `/suwagcms/*` to a trusted network only.

Without the runtime, the validator alone needs no database and no network:

```csharp
IReadOnlyList<Mistake> mistakes = DefinitionValidator.ValidateJson(json, ValidationStage.Version);
```

Pass a `BotDeclaration` as a third argument and the `Version` stage also reports what that bot cannot run; `Save` never looks at it.

It returns every mistake at once, each with a stable code and a fix; an empty list means the definition passes.
