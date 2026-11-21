# AGENTS.md

Rules for anyone — human or AI agent — changing this repository. `CLAUDE.md` only points here;
this file is the one to edit. Setup: [docs/development.md](docs/development.md). Not built yet: [TODO.md](TODO.md).

## Commands

- For people: a recipe in `justfile`, in a module of the whole repository beside it (`tmux-dev.just`), or in a tool's folder as `<folder>/mod.just`, with a one-line description; `just` lists them.
- For agents: a script in `scripts/.internal/`; each answers `--help`. A recipe only calls the script. The gate is
  `suwgate` (`Suwagcms.Web/node_modules/.bin/suwgate`), configured in `suwgate.yml`; `suwgate dev` runs the local stack.
- `suwgate check --quick` while working, and `suwgate step <step> [-- <tool args>]` runs one step while working (e.g. `suwgate step e2e -- --headed`
  after changing anything a user clicks; `suwgate step` lists them); `suwgate check` before calling a change done; it runs the browser tests too and needs
  the Playwright browser, and gives a linked worktree its own test databases and ports.
- Test last: finish every change of the task, then run the full gate once and push once, so CI runs once. After a red run, fix all it shows, rerun only the tests that failed, and the full gate once when they pass. Quick checks of the part you change (its unit tests, the type checker, `suwgate step <step>`) are fine while building.

## Git

- **The main branch is `master`**, never `main`.
- Work on a feature branch, never on `master`. Agents do not rewrite or discard history: no
  amend, reset, rebase, stash, force-push or deleting an unmerged branch. An agent may merge a pull request whose CI is green, and release.
- **A branch merged into `master` is deleted** right after the merge: local, remote and its worktree, with
  `git branch -d`, which refuses unmerged work. Needed again? Start a new one from `master`.
- A change with a red gate is not committed: it stays in the working tree and the summary says why.
- **One task, one branch.** Unrelated refactors, renames, version bumps and fixes go to their own
  branch or into `TODO.md`; say so once when you reach for one, and put it in if the owner confirms.
- Personal rules (how you commit, your tools) go in the gitignored `CLAUDE.local.md`, not here.

## Rules

- KISS and YAGNI first: the repo grows only when a real need shows up; one developer maintains it after hours. When in doubt, leave it out, and say in the change what was left out and why.
- Plan a change by listing what it makes obsolete, and delete that first, in the same change.
- Mutation tests (`just mutate`) run only when a person asks for them: an agent neither runs nor proposes them on its own.
- Correction or a new rule? Make the mistake impossible or loud first: a type → a lint → a test → a hook. Add a sentence here only for what none of these can check.
- A guard's message states the fix. Never weaken a guard to make a change pass: fix the change.
- No test promises speed, in milliseconds or in steps counted instead of them: a timing guard catches the clock, not a count, so a speed promise is dropped from its spec.
- Every test names an AC id, unless it is a guard in `Suwagcms.Gate`: a test without an id rots.

## Specs

A spec's acceptance criteria live as guardrails: the `criteria` step of `suwgate check` says what fits (the kinds of guardrail come with the profiles of `suwgate.yml`), its messages the fix.
What it cannot see:

- Set ids with `vdoc.py ids`, never by hand: a dropped id is never reused, an id is never renumbered.
- A criterion no automated check can prove is a wrong criterion: ask the owner, never keep it unguarded.
- A criterion gets the lightest check that can fail for it: a plain test before a server test, a server test before a browser test; browser tests cover what only a browser can.
- Before merging a spec'd feature, run `/suwagent:visual-guardrails`: the `criteria` step sees that an id is named, not that the test would fail.

## Review

A person also reads the diff only when the change touches the always-careful list: login and permissions,
payments, data migrations, code without characterization tests. The list is revised after incidents.

## Where code goes

[docs/architecture.md](docs/architecture.md). Read it before adding a folder, a project or a feature.
Docs say what and why, not where: a line that must change when the code moves does not belong in the docs.

## What no test can check

- Names are chosen word by word: domain words in the users' language (English here), software
  words in technical English. One word per thing, everywhere.
- Secrets never enter the repo: not in code, settings, tests or history. Local values live in
  user secrets or the gitignored `.env`; a real hit is removed and rotated, never allow-listed.

## No comments

No comments in project files: comments rot and excuse unclear code, so names, small functions, types and tests say what a comment would.

- Covered: every code and config file (C#, TypeScript, JavaScript, shell, YAML, Dockerfiles, ignore files, `.editorconfig`, MSBuild and XML files, JSON, SQL, CSS, justfiles), `///` docs, JSDoc, commented-out code and task markers included. Not covered: Markdown, `docs/` and data strings.
- Allowed, because a tool reads them (suwgate's comment guards hold the same list): every comment in `.env.example`; in a justfile, the line directly above a recipe or `mod`; a shebang; `# shellcheck <directive>=…`; `# syntax=…` in a Dockerfile; `// @ts-expect-error <reason>`, `// @vitest-environment <env>`; in C#, `/*lang=…*/` and `// <auto-generated>`; a comment that is exactly a criterion id where the `criteria` step reads it.
- `#pragma warning disable`, `@ts-ignore`, `@ts-nocheck`, `eslint-disable` and the null-forgiving `!` are banned outright.
- A reason that cannot live in code goes into a test, the spec, this file or `docs/` as a sentence.
