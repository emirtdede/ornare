# ADR 0007 — Use Windows system SQLite via winsqlite3

- Status: Accepted
- Date: 2026-09-24
- Requirements/Risks: ORN-SC-001, startup/package footprint, Native AOT qualification

## Context

Ornare is Windows 11-only and needs a small local metadata database. The convenience `Microsoft.Data.Sqlite` package normally brings a bundled SQLite native engine. Microsoft documents using `Microsoft.Data.Sqlite.Core` with SQLitePCLRaw bundles/providers when a different native SQLite engine is desired, including the `bundle_winsqlite3` path for Windows `winsqlite3.dll`. Microsoft Windows-app guidance notes that supported Windows provides SQLite and that using the system engine can reduce package size and receive OS servicing.

## Decision

Use:

- `Microsoft.Data.Sqlite.Core` 10.0.12 baseline;
- `SQLitePCLRaw.bundle_winsqlite3` 2.1.11 baseline (reconciled via ADR 0008);
- the bundle-supported initialization path before opening the first connection, verified against the pinned package behavior.

Do not bundle `e_sqlite3` in V1 by default.

## Consequences

- fewer Ornare-owned native binaries;
- Windows services the underlying SQLite engine;
- no dependency on SQLite extensions Ornare does not need;
- x64/ARM64 and Native AOT provider behavior must be proven in Gate 1;
- if a required capability is missing or compatibility evidence fails, changing providers requires a new/superseding ADR rather than an ad-hoc package change.
