# SQLite Provider Decision and Constraints

## Decision

Use:

- `Microsoft.Data.Sqlite.Core` 10.0.x pinned by the repository;
- `SQLitePCLRaw.bundle_winsqlite3` 3.0.x pinned by the repository;
- Windows system `winsqlite3.dll` as the native engine.

Do not reference the higher-level `Microsoft.Data.Sqlite` meta-package because it brings the bundled `e_sqlite3` engine by default.

## Why

Ornare is Windows-only. Using the Windows system SQLite engine reduces shipped native code, reduces duplicate native-runtime surface, and lets supported Windows servicing maintain the SQLite binary. Microsoft documents `bundle_winsqlite3` as the bundle targeting the Windows system SQLite library.

## Initialization

Bootstrap/vertical-slice must verify the selected package version's required initialization semantics before first `SqliteConnection`. Prefer the bundle's supported initialization path; do not mix bundle and manual provider initialization patterns accidentally.

At Gate 1 record:

- `SqliteConnection.ServerVersion`;
- effective provider/library loaded;
- x64 runtime result;
- ARM64 compile and real-hardware runtime result before release;
- Native AOT result if AOT remains the release target.

## Feature constraints

System SQLite feature compilation can differ from a bundled SQLite build. V1 storage therefore uses core relational SQL only and must not require optional FTS, JSON, R*Tree, loadable extensions, or custom native extensions. Any dependency on such a feature requires a platform-matrix proof and ADR.

## Security and reliability

- Parameterize SQL; never concatenate user-controlled paths/strings into SQL syntax.
- Use explicit schema versions and transactional migrations.
- Keep transactions short.
- WAL may be enabled only after packaged/local-state behavior is validated; handle `-wal`/`-shm` lifecycle correctly.
- Database durability never substitutes for the filesystem operation journal; arbitrary target-folder writes are outside SQLite's ACID transaction.
- Corrupt DB recovery must not delete folder-owned icons or blindly mutate user folders.
