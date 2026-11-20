#!/usr/bin/env bash
set -euo pipefail
if [ "${1:-}" = "--help" ]; then
    cat <<'HELP'
The fallback to `just sdk-publish`, which is the normal way, for a first release nuget.org's Trusted Publishing refuses.
Packs Suwagcms.Sdk as <version> on this machine and pushes it to nuget.org with SDK_NUGET_API_KEY from the gitignored .env.
Not for agents: a version on nuget.org cannot be deleted.
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
    echo "SDK_NUGET_API_KEY is not set: nothing was packed or pushed. Add it to .env at the repo root (a key from https://www.nuget.org/account/apikeys, scope Push, glob Suwagcms.Sdk), or release with just sdk-publish, which needs no key." >&2
    exit 1
fi

dotnet pack Suwagcms.Sdk -c Release -o .artifacts/nupkg -p:Version="$version" -p:ContinuousIntegrationBuild=true
echo "pushing Suwagcms.Sdk $version to nuget.org"
dotnet nuget push ".artifacts/nupkg/Suwagcms.Sdk.$version.nupkg" --source https://api.nuget.org/v3/index.json --api-key "$key"
