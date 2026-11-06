#!/usr/bin/env bash
set -euo pipefail
if [ "${1:-}" = "--help" ]; then
    cat <<'HELP'
Starts the release of Suwagcms.Sdk: runs the sdk workflow on master, which tests, packs and pushes to nuget.org.
Nothing is packed here and no key is used.
Not for agents: a version on nuget.org cannot be deleted.
HELP
    exit 0
fi
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"

if [[ $# -ne 1 || "$1" == -* ]]; then
    echo "usage: sdk-publish.sh <version>   (just sdk-publish <version>; docs/release.md)" >&2
    exit 2
fi
version="$1"
"$ROOT/scripts/.internal/sdk-version.sh" "$version"

cd "$ROOT"
if ! command -v gh >/dev/null 2>&1; then
    echo "gh is not installed: install the GitHub CLI (https://cli.github.com) and run gh auth login. Nothing was started." >&2
    exit 1
fi

# Why: the workflow builds origin/master, so these three make what is released the same as what is in front of the owner
branch="$(git branch --show-current)"
if [[ "$branch" != "master" ]]; then
    echo "on '${branch:-a detached HEAD}', and a release comes from master: run this in a checkout of master, after merging. Nothing was started." >&2
    exit 1
fi
if [[ -n "$(git status --porcelain)" ]]; then
    echo "the working tree has uncommitted changes, which the release would not contain: commit or remove them. Nothing was started." >&2
    exit 1
fi
git fetch --quiet origin master
if [[ "$(git rev-parse HEAD)" != "$(git rev-parse origin/master)" ]]; then
    echo "local master differs from origin/master, and the release is built from origin/master: push or pull until they match. Nothing was started." >&2
    exit 1
fi

gh workflow run sdk.yml --ref master -f version="$version"
echo "Started the release of Suwagcms.Sdk $version from $(git rev-parse --short HEAD). Follow it:"
echo "  gh run list --workflow sdk.yml --limit 1"
echo "  gh run watch"
