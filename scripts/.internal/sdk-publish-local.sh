#!/usr/bin/env bash
set -euo pipefail
if [ "${1:-}" = "--help" ]; then
    cat <<'HELP'
The fallback to `just sdk-publish`, which is the normal way, for a first release nuget.org's Trusted Publishing refuses.
Packs Suwagcms.Sdk and Suwagcms.Cli as <version> on this machine and pushes them to nuget.org with SDK_NUGET_API_KEY from the gitignored .env.
A version on nuget.org cannot be deleted, only unlisted: check the number first.
HELP
    exit 0
fi
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"

if [[ $# -ne 1 || "$1" == -* ]]; then
    echo "usage: sdk-publish-local.sh <version>   (just sdk-publish-local <version>; docs/release.md)" >&2
    exit 2
fi
version="$1"
"$ROOT/scripts/.internal/sdk-version.sh" "$version"

cd "$ROOT"
if [[ -f .env ]]; then
    # shellcheck disable=SC1091
    . ./.env
fi

key="${SDK_NUGET_API_KEY:-}"
if [[ -z "$key" ]]; then
    echo "SDK_NUGET_API_KEY is not set: nothing was packed or pushed. Add it to .env at the repo root (a key from https://www.nuget.org/account/apikeys, scope Push, glob Suwagcms.*), or release with just sdk-publish, which needs no key." >&2
    exit 1
fi

for package in Suwagcms.Sdk Suwagcms.Cli; do
    dotnet pack "$package" -c Release -o .artifacts/nupkg -p:Version="$version" -p:ContinuousIntegrationBuild=true
    echo "pushing $package $version to nuget.org"
    dotnet nuget push ".artifacts/nupkg/$package.$version.nupkg" --source https://api.nuget.org/v3/index.json --api-key "$key" --skip-duplicate
done
