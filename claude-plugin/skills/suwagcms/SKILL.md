---
name: suwagcms
description: Drive Suwagcms, the CMS of what a bot knows and does, through its suwagcms CLI. Use when the user wants to read or change a bot's definition (facts, knowledge bases, articles, procedures, actions, persona), its versions, pushes, history or availability in Suwagcms, register a bot, check the Suwagcms server, or asks to install or troubleshoot the suwagcms CLI.
---

# Suwagcms

Suwagcms keeps what a bot knows and does: facts, knowledge bases, articles, procedures, actions and a persona, edited on the web, through the `suwagcms` CLI or
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

A repository can say which CMS and bot it works with, as a committed file suwagcms/config.yaml under the folder a command runs in (`--dir <folder>` names another folder), so plain `suwagcms ...` needs no variable and no saved address. It holds `suwagcmsUrl` and `botId`, either may be left out; any other key, or a `--dir` with no such file, exits `2` and says the fix.

The server's address is the first one set of: `--server <url>` on a command, the `SUWAGCMS_URL` variable, `suwagcmsUrl` in that file, the address saved
with `suwagcms server <url>`, else `http://localhost:3330` (the web app of a laptop running `just web`). `suwagcms status` says
which address it used and whether the server answers.

The bot a command works on is the first one set of: `--bot <id>` on a command, the `SUWAGCMS_BOT` variable, `botId` in that file, the bot stored
with `suwagcms use <id>`. The choice belongs to the client: the server keeps no active bot, so another agent or a browser tab
working on a different bot does not change yours. A command that needs a bot and finds none exits `2` and lists the bots; a bot
that does not exist exits `1` and lists the bots that do. `suwagcms use` alone says which bot is in use and where it came from.
A bot's id (lowercase letters, digits and hyphens) is the name used everywhere and never changes.

## Output

Always pass `--json` when you read a result: it prints exactly one JSON document on stdout, also when the command fails, so
parse it and never the text meant for people. Without it, results go to stdout and problems to stderr.

Exit codes: `0` done, `1` the server refused the change, `2` the command was used wrongly (the usage is printed), `3` the
server cannot be reached or answered with something other than a refusal, `4` only from `apply --compare`: it found
differences. A `3` is for the user to fix (start the server, or give another address); do not retry in a loop.

## Refusals

Exit code `1` means the server refused the change and lists every mistake at once, each with a code, a message and its fix
(with `--json`, in `mistakes`). Fix all of them, then try once more. Do not work around a refusal and do not copy the server's
rules into your own: the refusal already says what is wrong and how to fix it.

## What would this change break?

`--dry-run` on an edit answers "what would this change break?" with the same refusal or answer as the real edit, and changes
nothing. An edit of a fact that would leave a step invalid is refused at that edit, naming the fact and every step that relies on it,
with no force option: `fact.enum.valueInUse` (a removed value a `when` or `assign` uses), `fact.range.inUse` (a narrowed `min`, `max` or `maxLength` that a value or bound falls outside), `fact.pattern.inUse` (a pattern a value in a `when` or `assign` does not match), `fact.writableBy.inUse` (`assign` on a fact a `collect` names) and `fact.type.inUse` (any change of type while a procedure uses the fact, even if each use would still fit). Only the server sees a type change, because it compares with the stored fact: the validator alone, offline, cannot. A global fact cannot be named `<procedureId>.<anything>` (`fact.name.procedurePrefix`).

## Never edit local files

A resource of the CMS (a fact, a knowledge base, an article, a procedure, an action, the persona) is never edited as a local file, and no file in the repository is its
copy. Change it only with the commands below, so the server's rules apply. The files `suwagcms export` writes are for reading:
changing them changes nothing.

## Show a page

`--open` opens the web page matching a command instead of printing. A command with no page refuses it with exit code `2`, and
it cannot be combined with `--json`. A command that changes one resource (`add` and `edit` of a kind, `persona edit`, `rename`, `availability on` and `off`) saves first and then opens the resource it saved; a refused change opens nothing, and `--open` with `--dry-run` exits `2`, since a dry run saves nothing. A command that deletes has no page, because what it deletes is gone. `bots add`, `versions create` and `push` change no one resource; `availability list`, `availability send` and `apply` reach several screens at once; `server`, `use` and `validate` concern no page. It only works from a shell that can reach the user's desktop:

- Run it as one command per Bash call, its output not piped or captured: a closed pipe kills it before the browser starts.
- Run it outside Claude Code's sandbox (the Bash tool's sandbox override, which the user approves), never in the background.
- Check it opened: the command prints the page's address; when you cannot be sure, give that address to the user as a link.

## Link to an article

In the `content` or `llmGuide` of an article, a link to another article of the bot is an ordinary Markdown link whose target is
`cms://article/<id>`, for example `[Price list](cms://article/cennik)`. The web shows it as a chip with the article's current title.

- Find the id with `suwagcms articles list --text <part of the title or id>`; the id is the first column.
- Write the link as text in `--content` or `--guide` (or their files) of `articles add` or `articles edit`; no other command is needed.
- A link to an article the bot does not have is refused with `reference.missing`, naming the field and the id; a malformed `cms://` target is refused with `reference.malformed`; a link that closes a loop of articles, or an article linking to itself, is refused with `reference.loop` and the path, such as `pricing → discounts → pricing`: remove one link on it. `--dry-run` shows it without saving.
- Text that only names an article stays plain text and is not protected. A `cms://` address inside a code span or code block, or an ordinary web link, is not a link to an article.

## Link to a procedure

In the `llmGuide` of an article, and only there, a link to a procedure is an ordinary Markdown link whose target is `cms://procedure/<id>`, for
example `If the customer wants a valuation, start [the valuation](cms://procedure/orderValuation).` The web shows it as a chip with the id.

- Find the id with `suwagcms procedures list`, and write the link as text in `--guide` (or its file) of `articles add` or `articles edit`.
- In `content`, which a customer reads, it is refused with `link.procedure.notInGuide`; a procedure the bot does not have with `reference.missing`; `conversation` with `link.procedure.conversation`.
- The link protects the procedure from being deleted, follows a change of its id, and a loop through it is refused (`reference.loop`).

## Commands

- `suwagcms status` - say which server address is used and whether the server answers; `--open` opens the web app.
- `suwagcms server <url>` - save the server address that later commands use.
- `suwagcms bots list` - list the registered bots, marking the one in use with `*`; `--open` opens the list in the web app.
- `suwagcms bots add <id> --name <name> --address <url>` - register a bot; it never contacts the address, so a bot can be registered before it runs.
- `suwagcms bots declaration [--stored]` - ask the bot what it supports (the format version it wants, the blocks it can run, the actions in its code, the embedding profiles it serves), store a good answer with its time and show it, with the procedures using a block it no longer declares. Exit code `1` when the bot cannot be reached or its answer is refused: the last good answer is shown with its time and the reason. `--stored` shows the last answer without asking. `--open` opens the bot's profile.
- `suwagcms use [<id>]` - store the bot later commands work on, or say which bot is in use.
- `suwagcms facts list` - list the global facts of the bot; `--open` opens the Facts screen in the web app.
- `suwagcms facts show <name>` - show one fact with its revision and the procedures that use it.
- `suwagcms facts add <name> --type enum|int|text --description <text> [--values a b | --min n --max n | --max-length n] [--pattern-regex <regex> --pattern-means <text> [--pattern-example <value>] [--pattern-tell-customer upfront|afterRefusal]] [--writable-by assign|set|correct]` - add a fact. An enum needs two or more values, none with a line break or a NUL and none the reserved `other`; an int both bounds, a text a maximum length above 0; `--writable-by` is `set` when left out. For a pattern and `--no-pattern` see "Text patterns".
- `suwagcms facts edit <name> [options of add] [--no-pattern] [--if-revision n]` - change a fact: only the options given change. Without `--if-revision` the edit applies to the revision it just read and a change made meanwhile is refused with the current revision.
- `suwagcms facts delete <name> [--if-revision n]` - delete a fact; refused with `reference.missing` while a procedure uses it, with every place that does listed in `related`.
- `suwagcms kb list` - list the knowledge bases of the bot (the groups its articles are in); prints nothing when there are none; `--open` opens the Knowledge bases screen in the web app.
- `suwagcms kb show <id>` - show one knowledge base with its language, embedding profile, results per search and revision.
- `suwagcms kb add <id> --title <text> [--language pl|en] [--embedding <profile>] [--results <n>]` - add a knowledge base. The id is camelCase and changes only with `suwagcms rename`; the title is unique within the bot, ignoring case and surrounding spaces. The language is that of every article in the base: `en` unless `--language pl` says otherwise, and any other value is refused. `--embedding` names the profile the bot embeds the base's articles with (`default` when left out): only a name, as the bot's environment holds its model, address and key; any non-empty name saves, and one the bot does not declare is refused when a version is created (`declaration.embeddingProfile`). `--results` is how many results one search gives from the base, 1 to 20 (`5` when left out; outside that `knowledgeBase.results.outOfRange`).
- `suwagcms kb edit <id> [--title <text>] [--language pl|en] [--embedding <profile>] [--results <n>] [--if-revision n]` - change the title, the language, the embedding profile or the results per search of a knowledge base: only the options given change (so `en` never overwrites `pl`, unless `--language en` is given); its id stays (`suwagcms rename kb` changes it). Changing the language changes it for every article in the base.
- `suwagcms kb delete <id> [--if-revision n]` - delete a knowledge base; refused while articles are in it, with the articles listed.
- `suwagcms articles list [--kb <id>] [--language pl|en] [--text <text>]` - list the articles of the bot, filtered, with the language of each (the language of its knowledge base); `--text` matches the title, id and description, ignoring case, never the content; `--open` opens the Articles screen in the web app, filtered the same.
- `suwagcms articles show <id>` - show one article in full with its revision; `--json` gives `content` and `llmGuide` exactly as stored.
- `suwagcms articles add <id> --kb <id> --title <text> --content <text>|--content-file <file> --guide <text>|--guide-file <file> --description <text>` - add an article. All options are required. An article has no language of its own: it is in the language of its knowledge base. `content` is what is true, in Markdown, and the customer reads it through the model; `llmGuide` is how the model should talk about it, in Markdown; `description` is plain text for the bot's search ("what does it answer"), never shown to the customer. A file may be `-` for stdin (one of the two). Texts are stored exactly as written. The id is camelCase, changes only with `suwagcms rename`, and is unique across all the bases of the bot.
- `suwagcms articles edit <id> [options of add] [--if-revision n]` - change an article: only the options given change; `--kb` moves it to another base, and the article then has that base's language. Without `--if-revision` the edit applies to the revision it just read and a change made meanwhile is refused with the current revision.
- `suwagcms articles delete <id> [--if-revision n]` - delete an article; refused with `reference.missing` while the `content` or `llmGuide` of another article links to it, with every such place listed in `related`; there is no force option, so remove the links first.

- `suwagcms procedures list` - list the procedures of the bot with what still stops each from becoming a version, and what stops the bot as a whole (no `conversation`); `--open` opens the Procedures screen in the web app.
- `suwagcms procedures show <id>` - print one procedure as YAML (`--json` for JSON), with its revision; what stops it from becoming a version is added as comments. Both forms are accepted back by `procedures edit --file`; `--open` opens it in the web editor instead.
- `suwagcms procedures add --file <file>` - add a procedure from a YAML or JSON file that holds the whole of it, id included. A step runs an action with `action`, `with`, `results`, `onFailure` and the optional `onUnavailable` (see Action step below). An unfinished procedure is saved and the answer lists what still stops a version; a mistake that makes it unsound is refused.
- `suwagcms procedures edit <id> --file <file> [--if-revision n]` - replace a procedure with the whole of the file (never a step at a time). The `revision` field of a file from `show` guards the edit; `--if-revision` wins over it. A file whose id differs from `<id>` is refused.
- `suwagcms procedures delete <id> [--if-revision n]` - delete a procedure; `conversation`, the base of every conversation, is always refused.
- `suwagcms actions list` - list the actions of the bot, HTTP and code in one list, with the implementation of each and whether the bot also declares an HTTP action's id or no longer declares a code action in use; `--open` opens the Actions screen in the web app.
- `suwagcms actions show <id>` - show one action as YAML (or JSON with `--json`) with its revision and the procedures that use it; both are accepted back by `actions edit --file`.
- `suwagcms actions add --file <file>` - add an action from a YAML or JSON file that holds the whole of it (see Action file below). A secret is written as a reference, `{secret:NAME}`, never as its value. The CMS never calls the action's URL: the bot does. `implementation: code` is refused with `action.code.readOnly`: a code action comes from the bot's declaration.
- `suwagcms actions edit <id> --file <file> [--if-revision n]` - replace an action with the whole of a file. A code action is read-only: change it in the bot's code and deploy (refused with `action.code.readOnly`).
- `suwagcms actions delete <id> [--if-revision n]` - delete an action that no procedure uses. A code action is read-only too: the CMS removes its copy once the bot stops declaring it and no step uses it.
- `suwagcms actions test <id> [--param name=value ...] [--yes]` - try a saved action through the bot, also one no version holds yet, and see the results or the failure with its reason. A test run really calls the service: every method but `GET`, and every code action, needs `--yes`; without it the command prints what would be called, sends nothing and exits `1`. `--json` gives `outcome` (`success`, `failure`, or `unreachable` or `notOffered`, where the action has not been judged), `results`, `reason`, `status` and `elapsedMs`. It exits `0` only on success. `--open` opens the action's page, with its test panel, and calls nothing.
- `suwagcms usage <kind> <id>` - with `<kind>` fact, article, procedure or action: show what uses a resource ("Used by") and what it uses ("Uses"), each with the field that holds the reference; `--json` gives both as data; `--open` opens the resource's page, which lists the same. Ask it before a delete; it changes nothing.
- `suwagcms rename <kind> <id> <newId> [--if-revision n] [--dry-run]` - with `<kind>` fact, kb, article, procedure or action: change a resource's id and rewrite every reference to it, all or nothing: a link's target in `content` and `llmGuide` (its label and all other text stay byte for byte), an article's `knowledgeBase`, a fact's name in each `collect`, `assign`, `when` and step mapping, and in each `{name}` placeholder of a `say` text that names it as a whole. Each rewritten resource gets a new revision, so a save based on an older one is refused; versions already created keep the old id, and plain text naming the resource is not changed. `conversation` cannot change its id. `--dry-run` lists each referrer and field; `--json` gives them as data; `--open` opens the resource at its new address.
- `suwagcms versions list` - list the versions of the bot, newest first, with date, counts of resources and digest; `--open` opens the Versions screen in the web app.
- `suwagcms versions create [--dry-run]` - validate the bot's whole working state and freeze it as the next numbered version, which never changes. Refused (exit code `1`) with every mistake while anything is unfinished, and when nothing has changed since the latest version. It never contacts the bot; `suwagcms push` sends it. `--dry-run` answers the same, creates nothing and uses no number.
- `suwagcms versions show <number>` - show one version with its document exactly as stored; `--json` gives it with its digest; `--open` opens the version's page.
- `suwagcms versions window <number> [--limit n] [--before id | --page n]` - list what happened between the previous version's creation and this version's, through its own `created` entry, newest first: every entry, each in one window at most, by the order they were recorded, also a push or a switch made in that time. A version whose window opens before history started says the list may be incomplete. A number the bot does not have is refused, naming its latest. `--json` gives the entries with `incomplete`; `--open` opens the version's page. For what a version's own creation and pushes were, use `suwagcms history version <number>`.
- `suwagcms versions status` - ask the bot which version it has, as the bot answers and never as the CMS last sent it: the latest version, the versions conversations still use, and the recent pushes with their reasons. An unreachable bot shows its last reported version and when; an address that answers as another bot names it.
- `suwagcms persona show` - print the persona of the bot as YAML with its line breaks and its revision, or say that it is empty; `persona edit --file` takes it back with every text unchanged; `--json` gives the texts exactly as stored; `--open` opens the Persona page in the web app.
- `suwagcms persona edit --file <file> [--if-revision n] [--dry-run]` - replace the whole persona with a YAML or JSON file (see Persona below). The `revision` field of a file from `show` guards the edit; `--if-revision` wins over it.
- `suwagcms apply <file> [--dry-run] [--replace] [--compare]` - apply a YAML or JSON file of changes all or nothing: every mistake comes back at once, each with its change's position in `change`, and the order inside the file does not matter. Start with `--dry-run`; the `suwagcms-batch` skill has the file's form and the steps. `--replace` makes an `add` of a resource the CMS already holds replace the whole of it, with every guard of an edit; one that would store what is stored is `unchanged` and moves no revision, so a seed can be applied again; nothing the file does not name is deleted. `--compare` changes nothing and says what `--replace` would do: each change added, replaced with the fields and steps that differ, or unchanged, and in `notMentioned` what the CMS holds of the same kinds that the file does not name; exit code `0` when nothing would change, `4` when something would, `1` when the file would be refused. Exit code `0` also for an empty list, `2` for an unreadable file, naming the line.
- `suwagcms availability list [--stored]` - list every article, procedure and action with whether it is on or off, and say whether the bot has the setting, from the bot's own answer; `--stored` lists what the CMS holds without asking the bot. A resource never touched is on; `--json` gives them as data.
- `suwagcms availability off <kind> <id> [--yes] [--dry-run]` - with `<kind>` article, procedure or action: turn a resource off; a live setting that takes effect at once, without a version or a push, also in conversations on older versions. It prints what uses the resource first: a resource in use needs `--yes`, and without it nothing changes and the exit code is `1`. References stay valid. If the bot does not confirm, the change stays saved, the exit code is `1` and the reason says what to do. `--dry-run` prints the list and what would change, and changes nothing. `conversation` cannot be turned off; facts and knowledge bases have no availability.
- `suwagcms availability on <kind> <id> [--dry-run]` - turn a resource on again, at once and with no question; the same delivery and exit code as `off`.
- `suwagcms availability send` - send everything that is off to the bot again, after it was not delivered; the CMS also retries by itself every minute until the bot confirms, and whenever the bot answers `suwagcms versions status` or the Versions screen with a lower number. Exit code `1` when the bot does not confirm. With several instances of a bot, the others pick up a change at their next poll.
- `suwagcms history [--limit n] [--before id | --page n]` - list every change to the bot, newest first, one page at a time (50 unless `--limit` says otherwise): when, what and the changed fields with their old and new values, also creating and pushing a version. `--before` takes the id the last page says to go on from; `--page` asks for a numbered page, as the web counts them, and its `--json` also has `total` and each change as `written`, as a comparison writes it; `--json` gives the entries; `--open` opens the bot's History page. It says what and when, never who, and no command can change it.
- `suwagcms history --kind <kind> [--limit n] [--before id | --page n]` - with `<kind>` fact, kb, article, procedure, action, persona or version: the same, only the changes of that kind of resource, every one of it, also deleted and renamed ones; `--kind version` lists the versions created and pushed, a rollback being a push of an older one. `--json` gives the entries as above; `--open` opens that kind's own screen, whose History panel lists the same. It cannot be used with `<kind> <id>`.
- `suwagcms history <kind> <id> [--limit n] [--before id | --page n]` - with `<kind>` fact, kb, article, procedure, action or version (`suwagcms history persona` takes no id): the same for one resource, also after it is deleted, with what it held last; `--open` opens the page of its history. A resource with no entries says no change has been recorded since history started.
- `suwagcms push [--version n] [--dry-run]` - push the latest version to the bot, which then reports the version it holds (`--json` gives the result, the reason and that version). A version the bot already holds is made current without being sent again. It asks for no confirmation: validation by the bot and rollback are the safeguards, so push when the work calls for it. Exit code `0` when the bot holds the version as current, also when it already had it (nothing is sent); `1` with the reason when the version does not exist (naming the nearest kept numbers), the bot cannot be reached or does not answer within 30 seconds, the bot refuses it with every mistake, or it names another version. A repeated push is safe.
- `suwagcms push --version <n>` - this is the rollback: push a kept version other than the latest. It changes only the bot: the working state and the latest version stay as they are, resources deleted since come back to customers but stay deleted in the CMS, and conversations in progress finish on their version; `suwagcms push` goes forward again. `--dry-run` says whether the version would be sent, made current or needs nothing, lists what it changes, and leaves the bot as it is. A bot holding the version on an SDK without the route fails saying to upgrade it.
- `suwagcms diff <from> <to>` - say what going from one version to another changes: each resource added, removed or changed, and in a changed one its fields with old and new values, `content` and `llmGuide` as lines removed and added, and a procedure's steps and local facts. It reads two frozen versions only, never the working state or the bot. Exit code `0` also when differences are found; `1` for a number the bot does not have. `--json` gives the same in a fixed order; `--open` opens the comparison in the web app.
- `suwagcms export [<number>] --out <folder>` - write a version (the latest when no number is given) to a new or empty folder of YAML files for reading and searching with ordinary tools: one file for the version itself and one per fact, knowledge base, article, procedure and action, in a folder per kind, and one for the persona. Changes to those files do nothing: no command takes them back. A folder with anything in it, or a number the bot does not have, is refused with exit code `1`; no `--out` exits `2`. `--json` gives the folder, the version and the count of files per kind; `--open` opens the version in the web app.
- `suwagcms validate --file <file> [--declaration <file>] [--stage save|version]` - check a definition (YAML or JSON, holding the whole bot and `formatVersion: 5`) with the same validator the CMS uses, without storing anything or contacting a bot: for a bot outside .NET. The Version stage unless told otherwise; `--declaration` also reports what that bot cannot run (the file holds what `GET /suwagcms/declaration` answers). Exit code `0` for no mistakes, `1` with every mistake otherwise.

`actions add`, `actions edit`, `actions delete`, `facts add`, `facts edit`, `facts delete`, `kb add`, `kb edit`, `kb delete`, `articles add`, `articles edit`, `articles delete`, `procedures add`, `procedures edit`, `procedures delete`, `persona edit`, `rename`, `apply` and `versions create` take `--dry-run`: it answers as the real command would, with the same refusals, and changes nothing. Use it to learn what a change would be refused for.

Every command takes `--json`, `--open`, `--server`, `--bot` and `--dir`; `suwagcms <command> --help` lists the rest.

## Text patterns

A text fact, global or local, can say what its values look like: `pattern: { regex: "[A-Z0-9]{4}/[0-9]{8}/[0-9]", means: "4 letters or digits, a slash, 8 digits, a slash and 1 digit", example: WA1M/00012345/6, tellCustomer: upfront }`.

- `regex` must match a whole value, and `means` says it in words; both are required. `regex` runs without backtracking, so lookarounds, backreferences, atomic groups and conditionals are refused (`fact.pattern.regexInvalid`), and it cannot match an empty text.
- `example` is optional, must match the regex and must fit `maxLength` (`fact.pattern.exampleTooLong`). `tellCustomer` is `upfront` (the default) or `afterRefusal`. An `int` or an `enum` has no pattern.
- A value in an `assign` or a `when` must match. A pattern change that a value no longer matches is refused with `fact.pattern.inUse`, naming the steps.
- `suwagcms facts edit <name> --no-pattern` takes it off, and `--pattern-regex`, `--pattern-means`, `--pattern-example` and `--pattern-tell-customer` change only what they give. In a file of changes, `pattern` is replaced whole and `pattern: null` takes it off.
- An enum never lists `other`: in every bot it stands for a value outside the list.

## Persona

A bot has one persona: `personality` says how it talks and `scope` what it talks about. Read it with `suwagcms persona show` and change it
with `suwagcms persona edit --file <file>`; the file, YAML or JSON, replaces the whole persona, so what it leaves out becomes empty:

```yaml
personality: |
  You are the customer service of a property appraiser.
  Answer in the language the customer writes in.
scope:
  inScope: |
    Property valuations by this appraiser
  outOfScope: |
    Legal, tax or investment advice
  whenOutOfScope: |
    Decline in one short sentence
```

- A YAML block such as `personality: |` keeps its line breaks, and every text is stored exactly as written. A text of only white space counts as empty; each is at most 20,000 characters (`persona.text.tooLong`). A field the format does not know is refused, naming it.
- It reaches the bot only with a version: after a save, `suwagcms versions create` and `suwagcms push`. An empty persona, or an empty part of one, is left out of the version.
- It cannot be renamed, deleted or turned off: to remove what it says, clear the fields. These texts are plain, so nothing in them refers to an article, a fact or a procedure.
- A bot that declares `persona: required` gets `persona.required` when a version without one is created or pushed; saves go on.
- A bot that declares `persona: complete` needs all four texts: each one left empty is a `persona.incomplete` when a version is created or pushed; saves go on.

## Before a push

Show what a push will change first: `suwagcms versions status` gives the version the bot has and the latest, and
`suwagcms diff <has> <latest>` compares them. Say what it changes for customers, then push.

## Action step

A procedure's step runs an action with the `action` block: `with` maps each parameter to the fact that gives its value, `results` maps a result to the fact it writes (one left out is ignored), `onFailure` names the step to go to when the action fails, `onUnavailable` (optional) the step to go to when the action is turned off, so the customer hears something other than to try again (without it the step takes `onFailure`), and `next` says where to go on success. On a failure no fact is written.

```yaml
checkAddress:
  action: checkServiceability
  with: { address: customer.address }
  results: { serviceable: isServiceable }
  onFailure: tellItFailed
  onUnavailable: tellSomeoneWillCall
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

A procedure is a graph of steps, which the web edits as a list; the file holds one whole procedure in YAML or JSON (the same structure). It belongs to `formatVersion: 5`, which a whole definition names and a procedure file does not. `id` is one camelCase word,
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
  `say: text` (send a fixed text), `assign: { fact: value }` (overwrite facts with values you chose), `start` and `inform` (below). `collect` cannot name a fact
  with `writableBy: assign`; every fact named must exist (`suwagcms facts list`) and a value must fit the fact's type.
- In a `say` text, `{name}` shows the value of the fact `name`, named as `collect` names it (a local fact by its name in the procedure), so the customer
  hears an action's result exactly: `say: Booked for {booking.date}, reference {bookingReference}.` The bot fills it in when it sends the text. A placeholder
  naming a fact the procedure does not see is refused (`reference.missing`); braces around anything else are plain text. Quote a text that starts with `{`.
  A placeholder is a use of its fact, as in `collect`: it blocks deleting the fact or changing its type, and `suwagcms rename fact` rewrites it.
- `start: <procedure id>` starts another procedure of this bot, and the step's `next` is where this one goes on once that one ends. The
  target must exist (`suwagcms procedures list`); `conversation` cannot be started (`start.target.conversation`), and a loop of starts is
  refused (`reference.loop`). A blank target is saved and listed as unfinished.
- `inform: { instruction: text, knowledge: [ ... ], points: { name: what must be said } }` has the model tell the customer each point. Where
  `say` sends a fixed text, inform leaves the words to the model, following the instruction, and the bot checks by name that each point was told.
  A point name is camelCase (`step.inform.pointName`), and the points keep the order written. No points (`step.inform.empty`), a point with no text
  (`step.inform.pointBlank`) or a blank instruction (`instruction.blank`) is saved and listed as unfinished. A point is fixed text and names no fact.
- `next` is a step's id, or a list of rules `{ when, to }` tried from the top; the last rule has no `when` and is the default, and only the last may (`next.default.notLast`).
  A step without `next` ends the procedure. Steps may loop back (a `conversation` returns to its own `listen`).
- The web's list is the same graph in other words, so nothing it does needs a file form of its own. Its `If` is a rule with `when` on the step above, its `Else` the last rule without `when`, its `Next:` row a `next` naming a step or a rule's `to`, its `End` a step with no `next`, an action's On failure branch `onFailure` and its When unavailable branch `onUnavailable`.
- `when` maps facts to one condition each: `oneOf: [a, b]` (enum, int, text), `range: { min: 1, max: 9 }` (int; one bound may be left out)
  or `known: true|false` (any fact).
- `whenStalled: { instruction: text }` is required when a step collects. It holds only its instruction: knowledge goes on an `inform`.
- `whenInterrupted: keep|discard` (optional, `keep` when left out) says what happens to this procedure when another one is started on top of it: it waits below, or it is removed with its local facts. Any other value (`procedure.whenInterrupted.invalid`) and the field on `conversation` (`procedure.whenInterrupted.onConversation`) are refused.
- The instruction of an `inform` can carry `knowledge: [{ knowledgeBase: id, articles: [id, ...], searchQueries: [text, ...] }]`: what the model
  should read before following it, one entry per knowledge base (a second entry for a base is refused, `knowledge.base.repeated`: merge them). A bot must declare that it reads knowledge, or a version holding any is refused
  (`declaration.knowledge`). A bot that declares `namedArticlesLimit` takes at most that many characters of named articles (content and guide) in one instruction, or the version is refused (`declaration.namedArticlesTooLong`): name fewer and add a search query. Name an article when you know which one holds the fact: it cannot miss, a wrong id is refused
  (`reference.missing`), an article outside the entry's base is refused (`knowledge.article.otherBase`) and the article is protected. Write a query when you want what the base says on a subject without knowing
  which article says it: the CMS stores it as text and never runs it, the bot searches. An entry with nothing in it (`knowledge.entry.empty`), a blank query (`knowledge.query.blank`) and a query on a
  base with no articles (`knowledge.query.noArticles`) are saved and listed as unfinished.
- `localFacts` (optional) maps a name to a fact that lives inside this procedure: `localFacts: { visitDate: { type: text, description: When to visit, maxLength: 20 } }`.
  The name is one camelCase word with no dots; the type, its constraints (`values` / `min` and `max` / `maxLength` and `pattern`), `description` and `writableBy` are those of a global fact. Steps of the
  procedure name it by that short name, and no other procedure sees it; `facts list` does not list it. A local fact with the name of a global fact is refused with `fact.local.nameClash`, from whichever side was saved second, and removing one that a step still uses is refused with `reference.missing`, listing the steps.
- Two stages: a mistake that makes the procedure unsound is refused (a step or fact that does not exist, an unwritable collect, a value the
  fact cannot take, a missing description); what only makes it unfinished (no `firstStep`, a step with no block, an unreachable step, rules
  without a default, a missing `whenStalled`, no `conversation`) is saved and listed after each save, so build a procedure step by step.

