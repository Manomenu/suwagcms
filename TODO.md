# TODO

## When suwgate has them: what the move to suwgate left behind

- Lookups that went with the deleted scripts: the guardrails of one criterion (`just criteria AC-…`) and the tests that pin another
  repository's contract (`just contracts`); and moving the pin of `vdoc.py` (`just criteria-update`), which suwgate holds now.

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

## The mistake codes as an enum of the contract

The refusal's top-level `code` is the enum `RefusalCode` in the contract; the mistakes' codes are still strings
(`ProducesRefusal<RefusalCode, string, …>`). They are a closed set, every one a constant of this repository
(`MistakeCodes` of the SDK, about 150, plus `StrictBody.UnknownField`, `ChangeList.OtherBot`, `ActionTester.ValueUnfit`), so by the
contract rule of AGENTS.md they belong in an enum too, which Suwbackkit 0.4 allows as a second type parameter
(`ProducesRefusal<RefusalCode, MistakeCode, RefusalMistake>`). Its own task, because `MistakeCodes` is the published SDK's API:
the bots compare against those constants, so the change is a major SDK release with a migration note, and the web's
`refusalReader<RefusalCode, MistakeCode>()` and its `mistake.code === "…"` checks follow.
