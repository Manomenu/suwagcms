# Architecture

## The map

One line per top-level path (at most 300 characters each). suwgate's `repository-layout` guard reads these
lines: a new top-level folder needs its line here, a deleted one loses it.

- `Suwagcms.slnx` — the .NET solution: the server, the SDK, the contracts, the CLI and their tests.
- `Suwagcms.Server/` — the HTTP API (ASP.NET Core minimal APIs, Npgsql, Dapper with hand-written SQL). One folder and namespace per feature: `Api/` wires the routes, `Health/` checks the database, `Database/` is the pool and the migrations, `Editing/` the one write path of every resource.
- `Suwagcms.Server/Database/Migrations/up/` — numbered SQL migrations, applied at start-up by grate and never edited afterwards.
- `Suwagcms.Server.Tests/` — xUnit v3 tests against a real PostgreSQL. Mirrors the server's folders; `Commands/` ties every route to a CLI command; `Characterization/` holds behaviour recorded before changing legacy code.
- `Suwagcms.Sdk/` — the SDK of a .NET bot, a NuGet package (`net9.0;net10.0`): the definition format, the validator, the bot runtime (routes, version store), JSON schemas (`just sdk-schema`). Contracts references it.
- `Suwagcms.Sdk.Tests/` — xUnit v3 plain tests of the SDK, no database. Mirrors the SDK's folders.
- `Suwagcms.Contracts/` — the records the API sends and receives, shared by the server and the CLI. One folder and namespace per feature, as in the server; references the SDK.
- `Suwagcms.Cli/` — the `suwagcms` command, packed as a .NET tool (System.CommandLine; `just cli-install`). One folder per feature, options and exit codes shared; references the contracts.
- `Suwagcms.Cli.Tests/` — xUnit v3 tests of the CLI, run in process on the streams it is given.
- `.claude-plugin/` — `marketplace.json`: this repository as a Claude Code marketplace, offering one plugin, `suwagcms`.
- `claude-plugin/` — the Claude Code plugin: `.claude-plugin/plugin.json`, and `skills/suwagcms/SKILL.md`, the main skill that lists every CLI command. Tests in `Suwagcms.Cli.Tests/Plugin/` keep the list and the CLI equal.
- `Suwagcms.Gate/` — the xUnit project that calls the .NET guards of NuGet `Suwgate.Architecture` and `Suwgate.Database` (`ArchitectureTests`), and this app's own guards suwgate does not have; `suwgate check --quick` runs it. See "The gate" below.
- `Suwagcms.Web/` — the web app (React 19, Tailwind 4, Vite, TypeScript strict, pnpm), with the gate `@maniumek/suwgate` among its packages. A folder per feature under `src/`; `src/shell/` the page frame, `src/design/` the design system, `src/api/` the HTTP client and generated types.
- `contract/` — the API contract (`openapi.json`) and its version (`package.json`), generated (`just api-types`) and committed for consumers.
- `compose.yaml` — the whole stack on a laptop from the same Dockerfiles: db (profile `db`, also the local database of `suwgate dev db`), server, web; everything configured from `.env` (`.env.example`) with ASP.NET names (`Section__Key`); ports only on 127.0.0.1 (db 5563, web 8220).
- `Suwagcms.Server/Dockerfile`, `Suwagcms.Web/Dockerfile` — the two images (pinned bases, locked restore, build context is the repo root); the web image is nginx serving the build and proxying `/api` to the server.
- `.dockerignore` — keeps `node_modules`, `bin`, `obj` and `.env` out of the build context.
- `.github/` — `workflows/ci.yml`: the gate (suwgate's reusable gate workflow), the browser tests (`e2e`), and the images built and smoke-tested (`build`); no image is published, the registry is project-specific. A second workflow, started by hand, releases the SDK to nuget.org.
- `.vscode/` — the editor's glue, shared: recommended extensions, formatter per language, the schema of `suwgate.yml`, file nesting, debug launch, tasks that run the gate. No style rules (they live in `.editorconfig` and the formatters' configs, so Rider agrees).
- `.githooks/` — `pre-commit` runs `suwgate hook pre-commit`: gitleaks on what is being committed, then the quick gate; `just sync` enables it.
- `docs/` — documentation for people working on the repo: this map, `development.md`, `release.md`, the specs in `visual/spec/` (their acceptance criteria live as guardrails, see AGENTS.md), decisions above one feature in `adr/`.
- `scripts/` — `scripts/.internal/` holds the tools of this app that `just` runs: `api-types.sh`, `e2e.sh`, `slow-tests.sh`, `sdk-schema.sh`, `sdk-version.sh`, `sdk-publish.sh`, `sdk-publish-local.sh`; the local stack is `suwgate dev`. The gate refuses a script that nothing calls.
- `.claude/` — the committed Claude Code settings: `suwgate hook stop` runs the quick gate when Claude stops, `suwgate hook guard-edit` protects migrations, generated files, and the gate with the lint configs (`ALLOW_GATE_EDIT=1 claude` lifts the last).
- `.config/` — `dotnet-tools.json`: the pinned CSharpier.
- `tmux-dev.just` — the `justfile` module of the local stack; a tool's own module is `<folder>/mod.just`.

Files at the root: `suwgate.yml` (the gate), `layers.json` (the server's layers and which project may reference which),
`Directory.Build.props` (the target framework, the settings NuGet reads before any package, and `Suwgate.Architecture`, which brings
the strict compiler, the analyzers and the banned APIs), `Directory.Build.rsp` (no idle MSBuild workers), `Directory.Packages.props`
(every package version, once), `.editorconfig` (whitespace and this app's own rule overrides), `global.json`, `nuget.config`,
`.gitattributes`, `.gitignore`.

## The gate

The gate is suwgate (npm `@maniumek/suwgate`, [its README](https://github.com/Manomenu/suwgate)): `suwgate check` runs the steps
of the profiles `suwgate.yml` names (web, dotnet, database) and its repository guards. This repository keeps only the wiring and what
is its own:

- `suwgate.yml`: the profiles, where the web app lives, the steps it skips and the exceptions, each with its reason, the edit hook's
  extra files, and the name and port offset of the local stack (`suwgate dev`: server 6330, web 3330, e2e 6331/3331, database 5563, stack 8220).
- `layers.json`: the order of the server's layers and which project may reference which; the app's own design.
- `Suwagcms.Gate/`: one call of `Guards.Verify` per .NET guard of `Suwgate.Architecture` and `Suwgate.Database`, with this app's arguments
  (its assemblies, layers file, solution, migrations folder, test fixture), and the guards of this app that suwgate does not have (below).
  It stays a project of its own, apart from the test projects, because `suwgate check --quick` runs every test of a project that uses
  those packages: here that is a few seconds, while the server tests need PostgreSQL and start the server.
- `Directory.Build.props` and `.editorconfig`: the build defaults, the banned APIs and the analyzer rules come from `Suwgate.Architecture`
  (buildTransitive props and a global analyzer config); the app keeps only what NuGet must read before any package, and its own overrides.
- The tool configs of the web app (`eslint.config.js`, `tsconfig*.json`, `prettier.config.js`, `knip.config.ts`,
  `e2e/playwright.config.ts`) extend suwgate's presets and keep only the app's paths, features and entry points.

An agent that cannot pass a guard must not be able to change it: the edit hook refuses `suwgate.yml`, `layers.json`, `Suwagcms.Gate/`,
the hooks, the workflows and the lint, type and build configs (suwgate's defaults; `Suwagcms.Gate/` is this app's line in `settings.hooks.protected`).
The owner lifts it for one session with `ALLOW_GATE_EDIT=1 claude`.

suwgate's `dotnet-test` step does not export the worktree's database names. The server tests' fixture drops and recreates its
database, so in a linked worktree it refuses to run on the main checkout's `suwagcms_test` until `TEST_CONNECTION_STRING` is set:
`just check` sets it (`suwgate dev checkout-env`) before `suwgate check`, and the refusal says how.

## What the gate forbids

Each line is a build rule, a test or a guard; its message says the fix. Rules live as low as they can: types and analyzers first.
suwgate's guards and steps are listed with their reasons in its README; the ones this app relies on:

- Web: tsc strict, eslint (typescript-eslint strict, feature boundaries, no browser storage, no comments), vitest, prettier (with Tailwind class order), knip, `openapi.d.ts` and `contract/openapi.json` equal to what the server generates, the contract's version right for what changed (oasdiff).
- Repository (suwgate guards): required files, AGENTS.md short and `CLAUDE.md` exactly `@AGENTS.md`, every path the docs name exists, every top-level folder in the map above, no vague folder, project or file names (Common, Utils, Services, Components…), every script called and answering `--help`, just modules, pinned images, compose ports on 127.0.0.1, no style keys in `.vscode/settings.json`, the hooks wired.
- Build (`Suwgate.Architecture`, and architecture/build-settings checks no project turns it off): analyzers `latest-all`, warnings are errors, unused private members (IDE0051, IDE0052, CA1823); no `#pragma warning disable`, `@ts-ignore` or `eslint-disable` (`no-in-place-switches`).
- Banned APIs (the list `Suwgate.Architecture` ships; a BannedSymbols.txt at the root would add the app's own lines): `DateTime.Now`/`UtcNow`, blocking on tasks, `Thread.Sleep`, `Console`, `Activator`, reflection discovery, `Type.GetType(string)`, Newtonsoft.
- Packages (`banned-nuget-packages`, `unused-nuget-versions`, `banned-npm-packages`): no MediatR, AutoMapper, Mapster, Scrutor, mocking libraries, FluentAssertions, Newtonsoft.Json or Entity Framework; every versioned package is referenced.
- Settings (`appsettings-bound`, `connection-host`): every section of appsettings is read by an options class; connection strings name `127.0.0.1`.
- Migrations (`migrations-unchanged`, database/migration-script-names): a script master has is never changed; names are `NNNN_snake_case.sql`.
- Layers (architecture/layers, architecture/endpoints): a layer uses only the layers below it, every namespace has a layer, every `*Endpoints` class is mapped from `Api`, one per namespace.
- Unlisted projects (architecture/every-project-guarded): a guard over a list covers nothing left off it; the SDK, the contracts and the CLI are flat, one folder per resource kind, and have no layers (exceptions in `suwgate.yml`).
- Project graph (architecture/project-references): which project may reference which and see whose internals.
- Names (architecture/unique-type-names, architecture/one-type-per-file, architecture/forbidden-type-names): unique type names, one top-level type per file named like the file; no namespace or type named with a vague word, no `*Repository`, `*Service`, `*UnitOfWork` types.
- Interfaces (architecture/interfaces-have-two-implementations): an interface has at least two implementations; a public interface of the SDK is the exception, its other implementations are the bots' stand-ins.
- Deleting (architecture/nothing-obsolete, architecture/no-unused-types): nothing `[Obsolete]`, no unused non-public type of the server.
- Escape hatches (architecture/no-escape-hatches): no `dynamic`, no null-forgiving `!`.
- Server tests (architecture/no-fixture-in-field): a test class takes the server from its class fixture, never starts one in a field.
- Comments (`comments-*` guards and suwgate's eslint rule `no-comments`): none in any code or config file, only the tool directives AGENTS.md lists ("No comments"). The contracts keep their `///` docs, which are the API's documentation in `contract/openapi.json` and the web app's types; `ContractDocsTests` refuses every other comment there.
- Time in tests (`TestTimingTests`): no test measures, waits on or limits real time: no `Stopwatch`, real clock, `Thread.Sleep`, `Task.Delay` outside a poll loop or `Timeout` in .NET tests; no `performance.now`, `Date.now()` arithmetic, `waitForTimeout`, `setTimeout` or own time limit in web tests. A retry or a timeout is proved with a fake `TimeProvider`.
- Worktrees (`LinkedWorktreeTests`): the server tests' fixture refuses the main checkout's test database in a linked worktree.
- Migrations at run time (`MigratorTests`): grate runs a script once, refuses a script edited after it ran, leaves nothing behind after a failing one, and leaves no temporary folders.
- Acceptance criteria (the `criteria` step, full gate only): each spec in `docs/visual/spec/` has an acceptance section with one unique `AC-<feature>-<n>` per criterion, each is named by a guardrail of a known kind, every id a guardrail names is in a spec, no id stands where the gate checks nothing.

## Disabled analyzer and lint rules, and why

`Suwgate.Architecture` turns on every analyzer (`AnalysisLevel` latest-all), makes each warning an error and ships the rules it
switches off, each with its reason, in its README; formatting is CSharpier's (140 columns). A rule that does not fit this app is
switched off in `.editorconfig`, one line each, never with `#pragma`, and gets its row here. The web's lint rules come from
suwgate's preset, with its reasons.

| Rule | Where | Why |
| --- | --- | --- |
| CA2100 | test projects | SQL built in a test (a database's name in `CREATE DATABASE`) is the test's own, never input |

The web's import boundaries (`app()` of `@maniumek/suwgate/eslint` in `eslint.config.js`): every folder of `src/` is a feature or a
shared folder, named there; a feature imports only itself and the shared folders (`api`, `design`), a shared folder only itself. The entry
files at the root of `src/` (`main.tsx`, `App.tsx`) put the page together and may import any feature.

Browser storage: what a screen shows lives in the address, so a link and a refresh give the same screen.

## Why some settings are as they are

- NuGet: `nuget.config` clears every source but nuget.org: with a second source (a company feed) central package management raises
  NU1507, and warnings as errors turn it into a failed build.
- Build workers: `Directory.Build.rsp` (`-nodeReuse:false`) and the MSBuild variables `suwgate dev checkout-env` exports leave no MSBuild
  workers or build server running after a build: parallel builds of several checkouts (agents in worktrees) otherwise pile up dozens of idle nodes.
- Line endings: `.gitattributes` keeps LF everywhere whatever the OS (as `.editorconfig`), never normalises binaries, and marks the lockfiles
  and the generated API types as generated, so GitHub collapses them in diffs.
- TypeScript: `tsconfig.json` is the browser app (suwgate's browser preset), `tsconfig.node.json` the code that runs in Node (the Vite
  and knip configs, the Playwright tests; its node preset). The app leaves out exactly the `*.e2e.ts` files the Node project checks.
- pnpm: `pnpm-workspace.yaml` holds pnpm 11's settings: esbuild (via Vite) needs its postinstall for its platform binary;
  `unrs-resolver` (from eslint-plugin-boundaries) gets its prebuilt binary as an optional package; `minimumReleaseAgeExclude` lets a new
  release of `@maniumek/suwgate` in on its first day.
- Vitest gives no test a time limit (`testTimeout: 0`): a loaded machine makes a test slower, never wrong, and the gate's step limit still stops a hang.
- The browser tests run with a page size of 5 (`PAGINATION_PAGE_SIZE`), so a test reaches a second page with few rows.
- The web image's nginx serves `/healthz` for the container's own liveness, independent of the API. `/api` is proxied through a
  variable so nginx looks the server's name up again every 10 s (a recreated server container gets a new address); with a variable and
  no URI in `proxy_pass` the request goes on unchanged, `/api` prefix included. The browser sees one origin, as behind the Vite proxy.
  Both images are built with the repository root as context: `docker build -f Suwagcms.Server/Dockerfile .`.
- The server image builds on the SDK 10.0.103: `global.json` admits only the 10.0.1xx band, and 10.0.103 is the newest image Microsoft
  publishes in it; the build stage ships no runtime.
- In `compose.yaml` every value comes from the environment (`.env`, see `.env.example`) with a default, so a bare
  `docker compose --profile db up` works; leave the `db` profile out and point `ConnectionStrings__Default` at an external PostgreSQL.
  Every port is on the loopback (the database password is a development one, and the server's routes have no key), which suwgate's
  `compose-ports` guard keeps.
- CI pins every action, and suwgate's reusable gate workflow, by its commit; a step's name carries the version the commit is. The
  reusable workflow has no database service, so the gate job starts the database of `compose.yaml` in its command before the gate.
- The SDK's release workflow (`sdk.yml`) runs only by hand (`just sdk-publish`), because a version on nuget.org cannot be deleted;
  nuget.org's trusted-publishing policy names its file, so it is not renamed alone. Its publish job runs no code of the repository.
- The CLI is a .NET tool named `suwagcms` on the base runtime only, though the SDK it references brings ASP.NET Core for bots. The SDK
  targets `net9.0` too, for bots not on .NET 10 yet; its licence is the SPDX expression MIT and its repository URL is written out, so
  Source Link does not take a developer's SSH remote.

## Decisions the code relies on

Reasons that used to sit in comments and that no name or test can carry.

- The OpenAPI build step starts the app only to read its routes, so start-up up to that point must not need a database.
- A server-test class starts one server, not one per test: xUnit creates the class anew for every test, and a server per test made
  the server tests about five times slower (architecture/no-fixture-in-field).
- The deletion guard (architecture/no-unused-types) gets `ServerApp` as an entry point, because the top-level `Program` that starts it has a
  generated `Main` the architecture reader does not see.
- The browser-test run (`e2e.sh`) drops and creates its database in two commands, because `CREATE DATABASE` cannot run inside the one
  transaction a single `-c` would be.
- A migration that already ran keeps its comment (`suwgate.yml` lets five through `comments-sql`): grate refuses a changed script.

## How code is split

The server and the web grow by feature: everything that makes one feature work sits in one folder named in the users'
terms, not by technical kind. Wiring is explicit, so grep finds every route.

- A new capability starts as a new folder, or goes into the feature it belongs to, never into a shared bucket. Shared
  code is earned by the second feature that needs it, and is named after what it is, not after who uses it.
- A new folder arrives with its first code.
- Rules live in plain modules with a test beside them; screens only wire them.
- The SDK is split by concept, not by spec feature: the format, the validator and the bot runtime, each with one folder
  per resource or mechanism, so a bot's author finds a type under what it is about. A spec feature cuts across them all.
- Everywhere (server, contracts, CLI, SDK, web, tests) a resource kind has one folder named after it in the plural, flat, one level deep:
  two kinds never share a folder under a common name (knowledge bases and articles are two folders, not `Knowledge`). A folder inside one
  appears only when it groups more than a name, as a procedure's blocks do.
- Records an endpoint sends or receives are a contract with the outside; a row from our own SQL is not. Keep them
  apart once they differ.
- A problem the caller can fix is a ProblemDetails whose `detail` says what to do.

## Areas (when a second product area appears)

Not used today: every feature sits directly under `Suwagcms.Server/` in one flat order. Do not
introduce an area for the first product area — only when a second one arrives.

An area groups the features of one product area (orders beside billing). It is one entry
in the top order and has an order of its own inside. Steps, each with the test that proves it:

1. Move the feature folders of the existing area under `Suwagcms.Server/<Area>/` (a move only, in a
   change of its own); namespaces follow the folders (IDE0130 fails the build otherwise).
2. In `layers.json` put the areas on one level of the layers of `Suwagcms.Server` (`"Orders | Billing"`).
   Proof: architecture/layers fails for an area missing from the layers.
3. Areas never use each other: siblings on one level are forbidden edges.
   Proof: make `Billing` call `Orders` and see architecture/layers fail.
4. What both areas need moves down into a shared feature below the areas (notifications
   between the areas and `Database`), and shared features never use an area (the same edges).
   The order of the features inside an area is not checked yet: architecture/layers reads the first namespace segment under
   the project only, so ask for nested layers in suwgate (`Suwgate.Architecture`) before an area grows an order of its own.
5. `Api/` stays above all areas and maps each area's routes in a method of its own, so grep finds them.
6. Web (phase 2): `src/<area>/<feature>/`, an `areaFeature` pattern in the `boundaries` rules of
   `eslint.config.js`, and an explicit policy for any edge between features of one area.
7. Add the area's folder to the map above in the same change.
