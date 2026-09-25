# ADR 0008 — Reconcile SQLitePCLRaw bundle_winsqlite3 package version

- Status: Accepted
- Date: 2026-09-24
- Requirements/Risks: ORN-SC-001, ORN-DAT-001, package restore integrity, Gate 0 readiness

## Context

During `/bootstrap` dependency verification against authoritative upstream sources (NuGet.org API and registry flatcontainer), the pinned package version `SQLitePCLRaw.bundle_winsqlite3` 3.0.5 was found to not exist in upstream NuGet feeds. NuGet package resolution returns `error NU1102: Unable to find package SQLitePCLRaw.bundle_winsqlite3 with version (>= 3.0.5)`. The latest published production stable version on NuGet is `2.1.11`.

## Decision

Reconcile the central package baseline for `SQLitePCLRaw.bundle_winsqlite3` from `3.0.5` to `2.1.11` across all baseline definitions:
1. `Directory.Packages.props`
2. `templates/Directory.Packages.props.reference`
3. `docs/VERSION_BASELINE.md`
4. `docs/OSS_REUSE.md`
5. `docs/adr/0007-use-windows-system-sqlite.md`
6. `THIRD-PARTY-NOTICES.md`

Maintain the architectural decision in ADR 0007: Ornare continues to use the Windows system `winsqlite3.dll` via `SQLitePCLRaw.bundle_winsqlite3` without bundling `e_sqlite3`.

## Consequences

- `dotnet restore` succeeds deterministically across all projects.
- No unverified or non-existent package versions remain in central package management.
- Zero known vulnerabilities: NuGet vulnerability feed confirmed clean for `SQLitePCLRaw.bundle_winsqlite3` 2.1.11.
- `scripts/spec-audit.ps1` remains 100% compliant.
