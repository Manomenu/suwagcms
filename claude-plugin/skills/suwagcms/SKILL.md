---
name: suwagcms
description: Drive Suwagcms, the CMS of what a bot knows and does, through its suwagcms CLI. Use when the user wants to read or change a bot's definition (facts, articles, procedures, versions) in Suwagcms, check the Suwagcms server, or asks to install or troubleshoot the suwagcms CLI.
---

# Suwagcms

Suwagcms keeps what a bot knows and does: facts, articles and procedures, edited on the web, through the `suwagcms` CLI or
by an agent. You drive it through the CLI, run through Bash. Anything the web can do, the CLI can do.
Source and docs: https://github.com/Manomenu/suwagcms

## Install

Check first: `command -v suwagcms && suwagcms --version`. `command not found`: the CLI is not installed. It is a .NET tool,
so it needs the .NET 10 SDK. Give the user the line to run, prefixed with `!` so it runs in this session's terminal:

```
! dotnet tool install -g Suwagcms.Cli
```

Update it later with `dotnet tool update -g Suwagcms.Cli`.

## Server and bot

The server's address is the first one set of: `--server <url>` on a command, the `SUWAGCMS_URL` variable, the address saved
with `suwagcms server <url>`, else `http://localhost:3330` (the web app of a laptop running `just web`). `suwagcms status` says
which address it used and whether the server answers.

The bot a command works on is the first one set of: `--bot <id>` on a command, the `SUWAGCMS_BOT` variable, the bot stored
with `suwagcms use <id>`. The choice belongs to the client: the server keeps no active bot, so another agent or a browser tab
working on a different bot does not change yours. A command that needs a bot and finds none exits `2` and lists the bots; a bot
that does not exist exits `1` and lists the bots that do. `suwagcms use` alone says which bot is in use and where it came from.
A bot's id (lowercase letters, digits and hyphens) is the name used everywhere and never changes.

## Output

Always pass `--json` when you read a result: it prints exactly one JSON document on stdout, also when the command fails, so
parse it and never the text meant for people. Without it, results go to stdout and problems to stderr.

Exit codes: `0` done, `1` the server refused the change, `2` the command was used wrongly (the usage is printed), `3` the
server cannot be reached or answered with something other than a refusal. A `3` is for the user to fix (start the server, or
give another address); do not retry in a loop.

## Refusals

Exit code `1` means the server refused the change and lists every mistake at once, each with a code, a message and its fix
(with `--json`, in `mistakes`). Fix all of them, then try once more. Do not work around a refusal and do not copy the server's
rules into your own: the refusal already says what is wrong and how to fix it.

## What would this change break?

`--dry-run` on an edit answers "what would this change break?" with the same refusal or answer as the real edit, and changes
nothing. An edit of a fact that would leave a step invalid is refused at that edit, naming the fact and every step that relies on it,
with no force option: `fact.enum.valueInUse` (a removed value a `when` or `assign` uses), `fact.range.inUse` (a narrowed `min`, `max` or `maxLength` that a value or bound falls outside), `fact.writableBy.inUse` (`assign` on a fact a `collect` names) and `fact.type.inUse` (any change of type while a procedure uses the fact, even if each use would still fit). Only the server sees a type change, because it compares with the stored fact: the validator alone, offline, cannot. A global fact cannot be named `<procedureId>.<anything>` (`fact.name.procedurePrefix`).

## Never edit local files

A resource of the CMS (a fact, an article, a procedure) is never edited as a local file, and no file in the repository is its
copy. Change it only with the commands below, so the server's rules apply. The files `suwagcms export` writes are for reading:
changing them changes nothing.

## Show a page

`--open` opens the web page matching a command instead of printing. A command with no page refuses it with exit code `2`, and
it cannot be combined with `--json`. It only works from a shell that can reach the user's desktop:

- Run it as one command per Bash call, its output not piped or captured: a closed pipe kills it before the browser starts.
- Run it outside Claude Code's sandbox (the Bash tool's sandbox override, which the user approves), never in the background.
- Check it opened: the command prints the page's address; when you cannot be sure, give that address to the user as a link.

## Link to an article

In the `content` or `llmGuide` of an article, a link to another article of the bot is an ordinary Markdown link whose target is
`cms://article/<id>`, for example `[Price list](cms://article/cennik)`. The web shows it as a chip with the article's current title.

- Find the id with `suwagcms articles list --text <part of the title or id>`; the id is the first column.
- Write the link as text in `--content` or `--guide` (or their files) of `articles add` or `articles edit`; no other command is needed.
- A link to an article the bot does not have is refused with `reference.missing`, naming the field and the id; a malformed `cms://` target, or `cms://procedure/…`, is refused with `reference.malformed`; a link that closes a loop of articles, or an article linking to itself, is refused with `reference.loop` and the path, such as `pricing → discounts → pricing`: remove one link on it. `--dry-run` shows it without saving.
- Text that only names an article stays plain text and is not protected. A `cms://` address inside a code span or code block, or an ordinary web link, is not a link to an article.

## Commands

- `suwagcms status` - say which server address is used and whether the server answers; `--open` opens the web app.
- `suwagcms server <url>` - save the server address that later commands use.
- `suwagcms bots list` - list the registered bots, marking the one in use with `*`; `--open` opens the list in the web app.
- `suwagcms bots add <id> --name <name> --address <url>` - register a bot; it never contacts the address, so a bot can be registered before it runs.
- `suwagcms bots declaration [--stored]` - ask the bot what it supports (the format version it wants, the blocks it can run, the actions in its code), store a good answer with its time and show it, with the procedures using a block it no longer declares. Exit code `1` when the bot cannot be reached or its answer is refused: the last good answer is shown with its time and the reason. `--stored` shows the last answer without asking. `--open` opens the bot's profile.
- `suwagcms use [<id>]` - store the bot later commands work on, or say which bot is in use.
- `suwagcms facts list` - list the global facts of the bot; `--open` opens the Facts screen in the web app.
- `suwagcms facts show <name>` - show one fact with its revision and the procedures that use it.
- `suwagcms facts add <name> --type enum|int|text --description <text> [--values a b | --min n --max n | --max-length n] [--writable-by assign|set|correct]` - add a fact. An enum needs two or more values, an int both bounds, a text a maximum length above 0; `--writable-by` is `set` when left out.
- `suwagcms facts edit <name> [options of add] [--if-revision n]` - change a fact: only the options given change. Without `--if-revision` the edit applies to the revision it just read and a change made meanwhile is refused with the current revision.
- `suwagcms facts delete <name> [--if-revision n]` - delete a fact; refused with `reference.missing` while a procedure uses it, with every place that does listed in `related`.
- `suwagcms kb list` - list the knowledge bases of the bot (the groups its articles are in); prints nothing when there are none; `--open` opens the Knowledge bases screen in the web app.
- `suwagcms kb show <id>` - show one knowledge base with its language and revision.
- `suwagcms kb add <id> --title <text> [--language pl|en]` - add a knowledge base. The id is camelCase and changes only with `suwagcms rename`; the title is unique within the bot, ignoring case and surrounding spaces. The language is that of every article in the base: `en` unless `--language pl` says otherwise, and any other value is refused.
- `suwagcms kb edit <id> [--title <text>] [--language pl|en] [--if-revision n]` - change the title or the language of a knowledge base: only the options given change (so `en` never overwrites `pl`, unless `--language en` is given); its id stays (`suwagcms rename kb` changes it). Changing the language changes it for every article in the base.
- `suwagcms kb delete <id> [--if-revision n]` - delete a knowledge base; refused while articles are in it, with the articles listed.
- `suwagcms articles list [--kb <id>] [--language pl|en] [--text <text>]` - list the articles of the bot, filtered, with the language of each (the language of its knowledge base); `--text` matches the title, id and description, ignoring case, never the content; `--open` opens the Articles screen in the web app.
- `suwagcms articles show <id>` - show one article in full with its revision; `--json` gives `content` and `llmGuide` exactly as stored.
- `suwagcms articles add <id> --kb <id> --title <text> --content <text>|--content-file <file> --guide <text>|--guide-file <file> --description <text>` - add an article. All options are required. An article has no language of its own: it is in the language of its knowledge base. `content` is what is true, in Markdown, and the customer reads it through the model; `llmGuide` is how the model should talk about it, in Markdown; `description` is plain text for the bot's search ("what does it answer"), never shown to the customer. A file may be `-` for stdin (one of the two). Texts are stored exactly as written. The id is camelCase, changes only with `suwagcms rename`, and is unique across all the bases of the bot.
- `suwagcms articles edit <id> [options of add] [--if-revision n]` - change an article: only the options given change; `--kb` moves it to another base, and the article then has that base's language. Without `--if-revision` the edit applies to the revision it just read and a change made meanwhile is refused with the current revision.
- `suwagcms articles delete <id> [--if-revision n]` - delete an article; refused with `reference.missing` while the `content` or `llmGuide` of another article links to it, with every such place listed in `related`; there is no force option, so remove the links first.

- `suwagcms procedures list` - list the procedures of the bot with what still stops each from becoming a version, and what stops the bot as a whole (no `conversation`); `--open` opens the Procedures screen in the web app.
- `suwagcms procedures show <id>` - print one procedure as YAML (`--json` for JSON), with its revision; what stops it from becoming a version is added as comments. Both forms are accepted back by `procedures edit --file`; `--open` opens it in the web creator instead.
- `suwagcms procedures add --file <file>` - add a procedure from a YAML or JSON file that holds the whole of it, id included. A step runs an action with `action`, `with`, `results` and `onFailure` (see Action step below). An unfinished procedure is saved and the answer lists what still stops a version; a mistake that makes it unsound is refused. `--open` opens the saved procedure in the web creator, laid out automatically when no one has placed its nodes.
- `suwagcms procedures edit <id> --file <file> [--if-revision n]` - replace a procedure with the whole of the file (never a step at a time). The `revision` field of a file from `show` guards the edit; `--if-revision` wins over it. A file whose id differs from `<id>` is refused.
- `suwagcms procedures layout <id> [--set <step>=<x>,<y>]... [--clear]` - show where the web creator draws each step, set some positions, or forget them all so the creator arranges the graph itself. Positions are no part of the procedure: no revision changes and no version holds them.
- `suwagcms procedures delete <id> [--if-revision n]` - delete a procedure; `conversation`, the base of every conversation, is always refused.
- `suwagcms actions list` - list the actions of the bot, HTTP and code in one list, with the implementation of each and whether the bot also declares an HTTP action's id or no longer declares a code action in use; `--open` opens the Actions screen in the web app.
- `suwagcms actions show <id>` - show one action as YAML (or JSON with `--json`) with its revision and the procedures that use it; both are accepted back by `actions edit --file`.
- `suwagcms actions add --file <file>` - add an action from a YAML or JSON file that holds the whole of it (see Action file below). A secret is written as a reference, `{secret:NAME}`, never as its value. The CMS never calls the action's URL: the bot does. `implementation: code` is refused with `action.code.readOnly`: a code action comes from the bot's declaration.
- `suwagcms actions edit <id> --file <file> [--if-revision n]` - replace an action with the whole of a file. A code action is read-only: change it in the bot's code and deploy (refused with `action.code.readOnly`).
- `suwagcms actions delete <id> [--if-revision n]` - delete an action that no procedure uses. A code action is read-only too: the CMS removes its copy once the bot stops declaring it and no step uses it.
- `suwagcms usage <kind> <id>` - with `<kind>` fact, article, procedure or action: show what uses a resource ("Used by") and what it uses ("Uses"), each with the field that holds the reference; `--json` gives both as data. Ask it before a delete; it changes nothing.
- `suwagcms rename <kind> <id> <newId> [--if-revision n] [--dry-run]` - with `<kind>` fact, kb, article, procedure or action: change a resource's id and rewrite every reference to it, all or nothing: a link's target in `content` and `llmGuide` (its label and all other text stay byte for byte), an article's `knowledgeBase`, a fact's name in each `collect`, `assign` and `when`. Each rewritten resource gets a new revision, so a save based on an older one is refused; versions already created keep the old id, and plain text naming the resource is not changed. `conversation` cannot change its id. `--dry-run` lists each referrer and field; `--json` gives them as data; `--open` opens the resource at its new address.
- `suwagcms versions list` - list the versions of the bot, newest first, with date, counts of resources and digest; `--open` opens the Versions screen in the web app.
- `suwagcms versions create [--dry-run]` - validate the bot's whole working state and freeze it as the next numbered version, which never changes. Refused (exit code `1`) with every mistake while anything is unfinished, and when nothing has changed since the latest version. It never contacts the bot; `suwagcms push` sends it. `--dry-run` answers the same, creates nothing and uses no number.
- `suwagcms versions show <number>` - show one version with its document exactly as stored; `--json` gives it with its digest.
- `suwagcms versions status` - ask the bot which version it has, as the bot answers and never as the CMS last sent it: the latest version, the versions conversations still use, and the recent pushes with their reasons. An unreachable bot shows its last reported version and when; an address that answers as another bot names it.
- `suwagcms apply <file> [--dry-run]` - apply a YAML or JSON file of changes all or nothing: every mistake comes back at once, each with its change's position in `change`, and the order inside the file does not matter. Start with `--dry-run`; the `suwagcms-batch` skill has the file's form and the steps. Exit code `0` also for an empty list, `2` for an unreadable file, naming the line.
- `suwagcms history [--limit n] [--before id]` - list every change to the bot, newest first, one page at a time (50 unless `--limit` says otherwise): when, what and the changed fields with their old and new values, also creating and pushing a version. `--before` takes the id the last page says to go on from; `--json` gives the entries; `--open` opens the bot's History page. It says what and when, never who, and no command can change it.
- `suwagcms history <kind> <id> [--limit n] [--before id]` - with `<kind>` fact, kb, article, procedure or version: the same for one resource, also after it is deleted, with what it held last; `--open` opens the page of its history. A resource with no entries says no change has been recorded since history started.
- `suwagcms push` - push the latest version to the bot, which then reports the version it holds (`--json` gives the result, the reason and that version). It asks for no confirmation: validation by the bot and rollback are the safeguards, so push when the work calls for it. Exit code `0` when the bot holds the version, also when it already had it (nothing is sent); `1` with the reason when the version does not exist, the bot cannot be reached or does not answer within 30 seconds, or the bot refuses it with every mistake. A repeated push is safe. Only the latest version can be pushed.
- `suwagcms diff <from> <to>` - say what going from one version to another changes: each resource added, removed or changed, and in a changed one its fields with old and new values, `content` and `llmGuide` as lines removed and added, and a procedure's steps and local facts. It reads two frozen versions only, never the working state or the bot. Exit code `0` also when differences are found; `1` for a number the bot does not have. `--json` gives the same in a fixed order; `--open` opens the comparison in the web app.
- `suwagcms export [<number>] --out <folder>` - write a version (the latest when no number is given) to a new or empty folder of YAML files for reading and searching with ordinary tools: one file for the version itself and one per fact, knowledge base, article and procedure, in a folder per kind. Changes to those files do nothing: no command takes them back. A folder with anything in it, or a number the bot does not have, is refused with exit code `1`; no `--out` exits `2`. `--json` gives the folder, the version and the count of files per kind; `--open` opens the version in the web app.
- `suwagcms validate --file <file> [--declaration <file>] [--stage save|version]` - check a definition (YAML or JSON) with the same validator the CMS uses, without storing anything or contacting a bot: for a bot outside .NET. The Version stage unless told otherwise; `--declaration` also reports what that bot cannot run (the file holds what `GET /suwagcms/declaration` answers). Exit code `0` for no mistakes, `1` with every mistake otherwise.

`actions add`, `actions edit`, `actions delete`, `facts add`, `facts edit`, `facts delete`, `kb add`, `kb edit`, `kb delete`, `articles add`, `articles edit`, `articles delete`, `procedures add`, `procedures edit`, `procedures delete`, `rename`, `apply` and `versions create` take `--dry-run`: it answers as the real command would, with the same refusals, and changes nothing. Use it to learn what a change would be refused for.

Every command takes `--json`, `--open`, `--server` and `--bot`; `suwagcms <command> --help` lists the rest.

## Before a push

Show what a push will change first: `suwagcms versions status` gives the version the bot has and the latest, and
`suwagcms diff <has> <latest>` compares them. Say what it changes for customers, then push.

## Action step

A procedure's step runs an action with the `action` block: `with` maps each parameter to the fact that gives its value, `results` maps a result to the fact it writes (one left out is ignored), `onFailure` names the step to go to when the action fails, and `next` says where to go on success. On a failure no fact is written.

```yaml
checkAddress:
  action: checkServiceability
  with: { address: customer.address }
  results: { serviceable: isServiceable }
  onFailure: tellItFailed
  next:
    - when: { isServiceable: { oneOf: [yes] } }
      to: offerPlan
    - to: sayNotServiceable
```

Types match strictly, judged on what the source can hold: an enum's values all among the target's, an int's range within the target's, a text's `maxLength` at most the target's, different types never (`action.mapping.tooWide`, `action.mapping.typeMismatch`). A result may write a fact with `writableBy: assign`. An unknown action or fact, or a parameter or result the action does not have, is `reference.missing`; two results into one fact is `action.mapping.resultTwice`. These refuse a save. A step with no `onFailure` or an unmapped parameter is saved and stops a version (`step.action.noOnFailure`, `step.action.unmappedParameter`). The same mapping mistakes on a code action only stop a version (`action.code.mapping`), since the bot's code can change without asking.

## Action file

An action is a contract and a call: the file holds one whole action in YAML or JSON. It never names a fact: a procedure's step maps its parameters and results.

```yaml
id: checkServiceability
description: Checks whether we serve an address.
parameters:
  address: { type: text, maxLength: 200, description: "Street, number and city." }
results:
  serviceable: { type: enum, values: [yes, no], description: Whether we serve it. }
implementation:
  http:
    method: POST                       # GET, POST, PUT, PATCH or DELETE
    url: https://crm.internal/check    # absolute; each {name} in it is a parameter
    timeoutSeconds: 20                 # 1 to 60, 10 when left out
    headers:
      Authorization: Bearer {secret:CRM_TOKEN}
```

A parameter or a result is typed like a fact (`enum` with `values`, `int` with `min` and `max`, `text` with `maxLength`), and each needs a description. A secret is a name resolved in the bot's environment: `{secret:CRM_TOKEN}` reads `SUWAGCMS_SECRET_CRM_TOKEN` there. It goes in a header only. A literal value in a header named like a credential (`Authorization`, `Cookie`, or holding `key`, `token`, `secret`, `password` or `auth`) or in a query parameter so named is refused with `action.secret.literal`; that net has holes, so never put a secret anywhere else either. `GET` and `DELETE` send the parameters not in the URL as a query string, the others as one JSON object; a result is the member of that name at the top level of the JSON answer.

## Procedure file

A procedure is a graph of steps; the file holds one whole procedure in YAML or JSON (the same structure). `id` is one camelCase word,
`description` is required, step names are camelCase. The procedure called `conversation` is the base of every conversation: a version
needs it, and a new bot has none, so write it. The smallest one:

```yaml
id: conversation
description: The base of every conversation.
firstStep: wait
steps:
  wait:
    listen:
    next: answer
  answer:
    say: Hello, how can I help?
    next: wait
```

- A step holds exactly one block: `listen` (wait for the next message), `collect: [fact, ...]` (ask until the facts are known),
  `say: text` (send a fixed text), `assign: { fact: value }` (overwrite facts with values you chose). `collect` cannot name a fact
  with `writableBy: assign`; every fact named must exist (`suwagcms facts list`) and a value must fit the fact's type.
- `next` is a step's id, or a list of rules `{ when, to }` tried from the top; the last rule has no `when` and is the default.
  A step without `next` ends the procedure. Steps may loop back (a `conversation` returns to its own `listen`).
- `when` maps facts to one condition each: `oneOf: [a, b]` (enum, int, text), `range: { min: 1, max: 9 }` (int; one bound may be left out)
  or `known: true|false` (any fact).
- `whenStalled: { instruction: text }` is required when a step collects.
- `localFacts` (optional) maps a name to a fact that lives inside this procedure: `localFacts: { visitDate: { type: text, description: When to visit, maxLength: 20 } }`.
  The name is one camelCase word with no dots; the type, its constraints (`values` / `min` and `max` / `maxLength`), `description` and `writableBy` are those of a global fact. Steps of the
  procedure name it by that short name, and no other procedure sees it; `facts list` does not list it. A local fact with the name of a global fact is refused with `fact.local.nameClash`, from whichever side was saved second, and removing one that a step still uses is refused with `reference.missing`, listing the steps.
- Two stages: a mistake that makes the procedure unsound is refused (a step or fact that does not exist, an unwritable collect, a value the
  fact cannot take, a missing description); what only makes it unfinished (no `firstStep`, a step with no block, an unreachable step, rules
  without a default, a missing `whenStalled`, no `conversation`) is saved and listed after each save, so build a procedure step by step.

