# Changelog

All notable product changes will be documented here using a human-readable release history.

## Unreleased — pre-implementation enterprise baseline

- Product/V1 specification, requirements traceability and 32 correctness invariants established.
- Antigravity rules, skills, workflows and Microsoft Learn MCP workspace configuration established.
- Premium WinUI 3 visual contract, anti-pattern rules, UI evidence rubric and design-freeze workflow established.
- Durable filesystem/SQLite operation journal, idempotent recovery and external-change ownership semantics specified.
- Windows 11 25H2 x64/ARM64 launch baseline and applicable 26H1 compatibility policy established.
- System-SQLite decision standardized on `Microsoft.Data.Sqlite.Core` + `SQLitePCLRaw.bundle_winsqlite3`.
- Native Explorer DLL hardening, secure SDLC, threat model, supply-chain controls, SBOM/provenance and fail-closed release gates established.
- Reset semantics corrected to merge only Ornare-owned icon semantics rather than restoring stale whole-file snapshots.
- Evidence paths standardized under `production_artifacts/`; gate status is `NOT PROVEN` when required evidence is absent.
- Dependency/verification scripts updated for .NET 10 noun-first package inspection and locked/no-restore audit behavior.
- Proprietary source ownership notice and pre-code third-party notice baseline added.

Production implementation has not started; no release claim is made by this changelog entry.
