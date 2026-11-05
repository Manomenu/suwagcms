#!/usr/bin/env bash
# Regenerate Suwagcms.Sdk/definition-format.schema.json from the records of Suwagcms.Sdk/Format and
# Suwagcms.Sdk/bot-runtime.schema.json from the state answer, the refusal and the version envelope.
# Run it after changing a record of the definition format, then commit the file: a test of
# Suwagcms.Sdk.Tests compares the committed schema with the format and fails when they differ.
#
#   sdk-schema.sh          rewrite the schema file
set -euo pipefail
if [ "${1:-}" = "--help" ]; then
    sed -n '2,/^[^#]/{/^#/s/^# \{0,1\}//p}' "$0"
    exit 0
fi
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
SUWAGCMS_WRITE_SCHEMA=1 dotnet test --project Suwagcms.Sdk.Tests --no-progress --filter-class '*SchemaTests'
echo "Wrote Suwagcms.Sdk/definition-format.schema.json and Suwagcms.Sdk/bot-runtime.schema.json"
