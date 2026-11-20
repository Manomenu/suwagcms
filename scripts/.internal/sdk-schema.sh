#!/usr/bin/env bash
set -euo pipefail
if [ "${1:-}" = "--help" ]; then
    cat <<'HELP'
Regenerate Suwagcms.Sdk/definition-format.schema.json from the records of Suwagcms.Sdk/Format and
Suwagcms.Sdk/bot-runtime.schema.json from the state answer, the refusal, the version envelope and the declaration.
Run it after changing a record of the definition format, then commit the file: a test of
Suwagcms.Sdk.Tests compares the committed schema with the format and fails when they differ.

  sdk-schema.sh          rewrite the schema file
HELP
    exit 0
fi
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
SUWAGCMS_WRITE_SCHEMA=1 dotnet test --project Suwagcms.Sdk.Tests --no-progress --filter-class '*SchemaTests'
echo "Wrote Suwagcms.Sdk/definition-format.schema.json and Suwagcms.Sdk/bot-runtime.schema.json"
