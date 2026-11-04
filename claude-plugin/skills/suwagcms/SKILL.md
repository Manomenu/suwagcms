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

## Never edit local files

A resource of the CMS (a fact, an article, a procedure) is never edited as a local file, and no file in the repository is its
copy. Change it only with the commands below, so the server's rules apply.

## Show a page

`--open` opens the web page matching a command instead of printing. A command with no page refuses it with exit code `2`, and
it cannot be combined with `--json`. It only works from a shell that can reach the user's desktop:

- Run it as one command per Bash call, its output not piped or captured: a closed pipe kills it before the browser starts.
- Run it outside Claude Code's sandbox (the Bash tool's sandbox override, which the user approves), never in the background.
- Check it opened: the command prints the page's address; when you cannot be sure, give that address to the user as a link.

## Commands

- `suwagcms status` - say which server address is used and whether the server answers; `--open` opens the web app.
- `suwagcms server <url>` - save the server address that later commands use.
- `suwagcms bots list` - list the registered bots, marking the one in use with `*`; `--open` opens the list in the web app.
- `suwagcms bots add <id> --name <name> --address <url>` - register a bot; it never contacts the address, so a bot can be registered before it runs.
- `suwagcms use [<id>]` - store the bot later commands work on, or say which bot is in use.
- `suwagcms facts list` - list the global facts of the bot; `--open` opens the Facts screen in the web app.
- `suwagcms facts show <name>` - show one fact with its revision.
- `suwagcms facts add <name> --type enum|int|text --description <text> [--values a b | --min n --max n | --max-length n] [--writable-by assign|set|correct]` - add a fact. An enum needs two or more values, an int both bounds, a text a maximum length above 0; `--writable-by` is `set` when left out.
- `suwagcms facts edit <name> [options of add] [--if-revision n]` - change a fact: only the options given change. Without `--if-revision` the edit applies to the revision it just read and a change made meanwhile is refused with the current revision.
- `suwagcms facts delete <name> [--if-revision n]` - delete a fact; it does not check yet whether a procedure uses it.

`facts add`, `facts edit` and `facts delete` take `--dry-run`: it answers as the real command would, with the same refusals, and changes nothing. Use it to learn what a change would be refused for.

Every command takes `--json`, `--open`, `--server` and `--bot`; `suwagcms <command> --help` lists the rest.
