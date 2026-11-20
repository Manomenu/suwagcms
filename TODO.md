# TODO

## When suwgate has them: what the move to suwgate left behind

- Lookups that went with the deleted scripts: the guardrails of one criterion (`just criteria AC-…`) and the tests that pin another
  repository's contract (`just contracts`); and moving the pin of `vdoc.py` (`just criteria-update`), which suwgate holds now.
- `suwgate check` applying `suwgate dev checkout-env` to its .NET tests: then `just check` calls `suwgate check` alone and the server
  tests' fixture keeps its refusal only as a guard.
- The `e2e` step creating its own empty database: then `e2e.sh` and the skip in `suwgate.yml` go, and CI's e2e job calls the step.
- Proposed for suwgate (with the reasons in the pull request that moved this repository): the test-timing guard (`TestTimingTests`,
  Manomenu/suwcrawlextract issue #2), a setting of `comments-csharp` for a project whose `///` docs are the API's documentation, `base`
  in file names allowed where architecture/forbidden-type-names allows it in type names, and architecture/every-project-guarded
  reading a project's `AssemblyName`. Each one that ships removes its exceptions from `suwgate.yml`.

## When the first spec's criteria need a strength check

The `criteria` step of `suwgate check` sees that a guardrail names a criterion, not that it would fail. The report that
measures it: run `just mutate` (Stryker.NET), read its JSON report (each mutant's `coveredBy` and
`killedBy` test ids), and count per `AC-<feature>-<n>` the mutants under the tests whose `Trait`
names it and how many survived. Stryker's `test-case-filter` does not work with the xUnit v3 runner
on Microsoft Testing Platform, so it reads a full run instead of running one criterion's tests.
First check that the `mtp` runner fills `coveredBy` per test.

## When the first fake of an outside service appears

The first fake in tests (WireMock for an external API, a mail catcher, e2e seed data) brings a
test environment of its own: a folder with its compose file, and a `just` module; `dev.compose` in `suwgate.yml`
points `suwgate dev db` there. Until then the gate needs only the one PostgreSQL container of `compose.yaml`.
