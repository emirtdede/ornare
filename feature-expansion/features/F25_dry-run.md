# F25 — Dry Run

**Status:** IMPLEMENT  
**Dependencies:** F24; shared operation planning  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Produce the same validation/operation plan a real command would use while guaranteeing zero durable/visible mutation and zero hidden cache/settings/access-time writes.

## Fixed decisions

1. Dry run is planning; it is never implemented as perform-and-rollback.
2. `--dry-run` is accepted exactly by F24 commands `apply`, `apply-last`, `reset`, `undo`, `export-ico`, `export-png`.
3. `inspect` is intrinsically read-only and **rejects `--dry-run` with exit code 2** rather than exposing a meaningless duplicate mode.
4. Dry run may read target/config/source/database metadata and may decode an input only when decoding is required to determine validity; it never writes journal, cache, thumbnail, settings, recent colors, Last Applied, DB access timestamps, database rows, icon files, export files, `desktop.ini`, attributes or shell state.
5. Storage in DryRun mode opens SQLite using an explicitly **read-only connection** (`Mode=ReadOnly` or the existing equivalent) and repositories expose only read methods.
6. Imaging DryRun cannot populate caches; any decode/render validation result is memory-only and disposed.
7. Output states are exactly `WouldSucceed`, `WouldFail`, `IndeterminateUntilCommit`; future races, free-space changes and commit-time shell conditions remain explicitly indeterminate.
8. The planner used by DryRun is the same preparation/validation phase used by real execution; real commit always revalidates.

## Integration points

- `Ornare.Core`: immutable `OperationPlan`, shared Prepare/Commit split and `ExecutionMode`.
- `Ornare.Storage`: read-only connection/repository path.
- `Ornare.Imaging`: no-write validation path.
- `Ornare.Windows`: read-only eligibility/identity/config probes.
- `Ornare.Cli`: exact flag exposure from Fixed Decision 2.

## Implementation sequence

1. Inventory every write side effect in apply/apply-last/reset/undo/export, including non-obvious cache/settings/recent/access-time updates.
2. Refactor only as needed so real execution is `Prepare -> Revalidate -> Commit`; DryRun executes `Prepare` and read-only probes only.
3. Propagate explicit `ExecutionMode.DryRun`; write-capable service entry points reject that mode defensively.
4. Open Storage read-only for DryRun and disable all automatic write-on-read behaviors.
5. Build `OperationPlan` with target identity snapshot, intended actions, cache hit/miss expectation, expected artifact sizes when known, typed blockers and indeterminate commit conditions.
6. Add write-denial test adapters at filesystem/database/settings/cache/shell-notification boundaries; any write call fails the test.
7. For export dry run, validate destination/path capability/permissions as far as safely observable but create no file/temp.
8. JSON exposes structured plan/actions/blockers; no image bytes/secrets.
9. Real operation after a successful dry run still revalidates all mutable assumptions immediately before commit.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| Hidden DB write on read | Read-only SQLite connection + write-denial instrumentation. |
| Cache/settings mutation | DryRun renderer/settings interfaces reject writes; snapshot tests. |
| Dry run diverges from real validation | Shared Prepare path, not cloned rules. |
| Plan overpromises | Explicit `IndeterminateUntilCommit`; real commit revalidates. |
| Export dry run creates temp | Destination probe only; file API write adapter must remain unused. |

## Required tests

- For each supported command, compare filesystem tree metadata/content hash, SQLite DB/WAL/SHM state, settings, caches, history/journal, shell-notification spy before/after DryRun: zero mutation.
- `inspect --dry-run` -> argument error exit 2.
- Stable fixture: DryRun blocker/status matches real operation's Prepare result.
- TOCTOU fixture: target changes after DryRun and real commit safely revalidates/fails.
- Decode-required invalid input is rejected without cache/temp writes.
- JSON plan schema snapshot tests.

## Performance requirements

- No extra service/background process.
- Do not perform expensive full final render solely for DryRun if format/header/decode validation is sufficient to decide Prepare; if exact encoded size is unknown, report it unknown rather than rendering just to estimate.
- Read-only connection/setup must not regress normal CLI startup when `--dry-run` is absent.

## Approved sources / research

- **System.CommandLine:** https://github.com/dotnet/command-line-api
- **Microsoft.Data.Sqlite connection strings:** https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings

## Forbidden implementations

- No perform-then-rollback.
- No writable DB connection in DryRun.
- No cache/recent/Last-Applied/access-time write.
- No duplicated/fake business validation.
- No skipping real commit revalidation because DryRun passed.

## Feature Definition of Done

- Write-denial instrumentation proves zero mutation across all six supported command families.
- Prepare/real-operation equivalence fixtures green.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F25/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
