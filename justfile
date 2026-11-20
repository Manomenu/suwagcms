set shell := ["bash", "-euo", "pipefail", "-c"]

suwgate := "Suwagcms.Web/node_modules/.bin/suwgate"

# The local stack in one tmux session: a shell, the server with its database, the web app — `just tmux-dev start [--restart]|stop` (tmux-dev.just)
mod tmux-dev

[private]
default:
    @just --list --unsorted

# The local PostgreSQL of compose.yaml on 127.0.0.1:5563: up, down, reset, psql, logs, status
[group('run')]
db *args:
    @{{ suwgate }} dev db {{ args }}

# HTTP API on :6330, rebuilt on every change — needs `just db up`; its JSON log is also written to .artifacts/logs/server.jsonl
[group('run')]
server *args:
    @{{ suwgate }} dev server {{ args }}

# Why did it fail? The last warnings and errors of the newest server log, one line each (--all, --trace <id>, --compose, -n N)
[group('run')]
[positional-arguments]
logs *args:
    @{{ suwgate }} dev logs "$@"

# Web UI on :3330, proxies /api to the server — run `just server` alongside
[group('run')]
web *args:
    cd Suwagcms.Web && { [ -d node_modules ] || pnpm install; } && pnpm dev {{ args }}

# Build and start the whole stack with its PostgreSQL on http://127.0.0.1:8220 (WEB_PORT in .env); waits until every service is healthy, then smoke-tests it
[group('infra')]
up:
    docker compose --profile db up -d --build --wait
    {{ suwgate }} dev smoke http://127.0.0.1:${WEB_PORT:-8220}

# Stop the stack (images and the database volume stay)
[group('infra')]
down:
    docker compose --profile db down --remove-orphans

# Regenerate the API contract (contract/openapi.json) and the web app's types from it, after changing a record the API sends or receives; then raise the contract's version in contract/package.json
[group('dev')]
api-types:
    @./scripts/.internal/api-types.sh

# Browser tests (Playwright) against a real server and an empty database of their own; not part of `just check`
[group('maintenance')]
e2e *args:
    @./scripts/.internal/e2e.sh {{ args }}

# The whole gate: build with analyzers, csharpier, every test, the web checks, the API contract and its version, acceptance criteria, shellcheck, gitleaks (the same CI runs); a linked worktree gets its own test database
[group('maintenance')]
check:
    @eval "$({{ suwgate }} dev checkout-env)" && {{ suwgate }} check

# The quick gate: the repository guards, and build, csharpier, eslint, tsc, shellcheck for what changed (what the Claude Code Stop hook runs)
[group('maintenance')]
check-quick:
    @{{ suwgate }} check --quick

# Which .NET tests take the longest: runs them all once and lists the slowest (-n 25 for more); a report, never a check
[group('maintenance')]
[positional-arguments]
slow-tests *args:
    @./scripts/.internal/slow-tests.sh "$@"

# Format the code: CSharpier for C#, Prettier for the web app
[group('maintenance')]
fmt:
    dotnet tool restore >/dev/null && dotnet csharpier format .
    cd Suwagcms.Web && pnpm exec prettier --write --log-level warn .

# Mutation testing of the server code changed since master (Stryker.NET): a surviving mutant is a test that is too weak
[group('maintenance')]
mutate:
    @{{ suwgate }} dev db up >/dev/null
    dotnet tool restore >/dev/null
    cd Suwagcms.Server.Tests && dotnet tool run dotnet-stryker

# Regenerate Suwagcms.Sdk/definition-format.schema.json after changing a record of the definition format
[group('dev')]
sdk-schema:
    @./scripts/.internal/sdk-schema.sh

# Release Suwagcms.Sdk as <version>: runs the sdk workflow on master, which pushes to nuget.org (docs/release.md)
[group('dev')]
sdk-publish version:
    @./scripts/.internal/sdk-publish.sh {{ version }}

# The fallback: pack and push from this machine with a key from .env, for a first release the workflow cannot make (docs/release.md)
[group('dev')]
sdk-publish-local version:
    @./scripts/.internal/sdk-publish-local.sh {{ version }}

# Pack the CLI and install (or update) it as the global `suwagcms` command
[group('dev')]
cli-install:
    dotnet pack Suwagcms.Cli -c Release -o .artifacts/nupkg
    dotnet tool uninstall --global Suwagcms.Cli || true
    dotnet tool install --global --add-source .artifacts/nupkg Suwagcms.Cli

# Install tools and packages from the lockfiles, and enable the git hooks
[group('maintenance')]
sync:
    git config core.hooksPath .githooks
    dotnet tool restore
    dotnet restore Suwagcms.slnx
    cd Suwagcms.Web && pnpm install --frozen-lockfile
