# AGENTS.md

Rules for anyone — human or AI agent — changing this repository. `CLAUDE.md` only points here;
this file is the one to edit. Setup: [docs/development.md](docs/development.md). Not built yet: [TODO.md](TODO.md).

Read @AGENTS.shared.md first: the rules every repository of the stack shares. This file adds this repository's own and wins where it says otherwise.

## Commands

- For people: recipes also live in a module of the whole repository beside `justfile` (`tmux-dev.just`) or in a tool's folder as `<folder>/mod.just`.
- For agents: a script in `scripts/.internal/`, which a recipe calls. The gate is
  `suwgate` (`Suwagcms.Web/node_modules/.bin/suwgate`), configured in `suwgate.yml`; `suwgate dev` runs the local stack.
- `suwgate step e2e -- --headed` after changing anything a user clicks. The full `suwgate check` runs the browser tests too, needs
  the Playwright browser, and gives a linked worktree its own test databases and ports.

## Rules

- Every test names an AC id, unless it is a guard in `Suwagcms.Gate`: a test without an id rots.
- Mutation tests run with `just mutate`.
- Names: the users' language here is English.

## Specs

A criterion gets a plain test before a server test, a server test before a browser test. The kinds of guardrail come with the profiles of `suwgate.yml`.

## Review

The always-careful list: login and permissions, payments, data migrations, code without characterization tests.
