# Ornare Architecture Report — Gate 2 (Core V1 Product & Manager Experience)

- **Phase**: Gate 2 — Core V1 Product & Manager Experience
- **Date**: 2026-09-25
- **Evaluator**: Antigravity Autonomous Architecture Reviewer
- **Target OS Baseline**: Windows 11 25H2 (x64 validated, ARM64 compile validated)
- **Status**: **VERIFIED**

---

## 1. Architectural Scope and Discipline

Gate 2 expands Ornare from the minimal single-folder vertical slice (Quick Panel + Explorer Extension) into the complete, production-grade V1 local management system. In strict adherence to `AGENTS.md` and Gate 2 rules:

- **Zero Feature Creep**: Prohibited out-of-scope capabilities (batch operations, folder templates, stickers, emoji, text-on-folder, online icon marketplace, web search, AI generation, auto cover detection, recursive processing, automation rules, cloud sync, accounts, telemetry, ads, subscriptions, image context menu, Ctrl+K command palette, plugin engines) have been 100% excluded.
- **No Architectural Bloat**: No extraneous enterprise layers were introduced. Existing project abstractions in `Ornare.Core`, `Ornare.Storage`, `Ornare.Windows`, and `Ornare.App` were extended directly without mediator, event bus, dynamic reflection, or DI complexity.
- **Native Authoring**: All C# and XAML components were hand-authored without code generation scripts.

---

## 2. Project Abstractions & Domain Boundaries

The Ornare solution maintains strict decoupled boundaries across projects:

```
┌────────────────────────────────────────────────────────┐
│                      Ornare.App                        │
│   (WinUI 3 Host, QuickPanelWindow, ManagerWindow)      │
└────────────┬──────────────┬──────────────┬─────────────┘
             │              │              │
             ▼              │              ▼
┌───────────────────────┐   │   ┌────────────────────────┐
│  Ornare.DesignSystem  │   │   │     Ornare.Windows     │
│ (Tokens, Mica, Theme) │   │   │(FolderCustomizer,      │
└───────────────────────┘   │   │ FolderHealthEngine,    │
                            │   │ RepairEngine, Identity)│
                            │   └──────────┬─────────────┘
                            ▼              │
             ┌─────────────────────────┐   │
             │       Ornare.Core       │◄──┘
             │(Models, Diagnostics,    │
             │ Invariants, Constants)  │
             └──────────────┬──────────┘
                            │
             ┌──────────────┴──────────┐
             ▼                         ▼
┌─────────────────────────┐ ┌────────────────────────────┐
│      Ornare.Imaging     │ │       Ornare.Storage       │
│(Magick.NET, Multi-Res   │ │(SqliteStorageService, WAL, │
│ ICO, Contain Normalize) │ │ Assets, History, Settings) │
└─────────────────────────┘ └────────────────────────────┘
```

### Module Responsibilities:

1. **`Ornare.Core`**:
   - Immutable domain models: [`LibraryAsset`](file:///c:/Users/emir/Desktop/Ornare/src/Ornare.Core/LibraryAsset.cs), [`FolderAssignment`](file:///c:/Users/emir/Desktop/Ornare/src/Ornare.Core/FolderAssignment.cs), [`AssignmentHistoryEntry`](file:///c:/Users/emir/Desktop/Ornare/src/Ornare.Core/AssignmentHistoryEntry.cs), [`UserSettings`](file:///c:/Users/emir/Desktop/Ornare/src/Ornare.Core/UserSettings.cs).
   - Invariant definitions: `OrnareConstants`, `FolderCustomizationState`, `FolderIconSemantics`.
   - Privacy & Diagnostics engine: [`DiagnosticExportService`](file:///c:/Users/emir/Desktop/Ornare/src/Ornare.Core/DiagnosticExportService.cs) with regex user path anonymization (`INV-DIAG-001`).

2. **`Ornare.Storage`**:
   - Single unified SQLite database located in `%LocalAppData%\Ornare\ornare.db`.
   - Durable ACID persistence in `WAL` journal mode with `synchronous = NORMAL`.
   - Deterministic asset deduplication via SHA-256 (`INV-LIB-001`).
   - Unified favorite and recent history tracking across Quick Panel and Manager (`INV-FAV-001`).

3. **`Ornare.Windows`**:
   - Win32 platform integration: [`FolderIdentityService`](file:///c:/Users/emir/Desktop/Ornare/src/Ornare.Windows/FolderIdentityService.cs) querying volume serial and 64-bit file ID via `GetFileInformationByHandle`.
   - Read-only health evaluation: [`FolderHealthEngine`](file:///c:/Users/emir/Desktop/Ornare/src/Ornare.Windows/FolderHealthEngine.cs).
   - Safe icon repair engine: [`RepairEngine`](file:///c:/Users/emir/Desktop/Ornare/src/Ornare.Windows/RepairEngine.cs) utilizing Gate 1 durable mutation infrastructure; refuses silent overwrite of external icon conflicts (`INV-REP-001`).

4. **`Ornare.Imaging`**:
   - Secure image decoding and 10-frame multi-resolution ICO generation (16px to 256px) with square containment and alpha preservation (`INV-IMG-001` through `INV-IMG-004`).

5. **`Ornare.App`**:
   - WinUI 3 desktop client featuring dual execution modes:
     - **Quick Panel Mode** (`--set-folder <path>`): Lightweight (420x330px), drag-and-drop icon application with secondary recent icons strip and direct Manager navigation affordance.
     - **Manager Mode** (`--manager` or primary launch): 920x640px continuous Mica surface with 5 primary sections: Library, Recent, Favorites, Customized Folders, Settings.

---

## 3. SQLite V1 Data Model & Migrations

The database schema is fully managed in [`SqliteStorageService.cs`](file:///c:/Users/emir/Desktop/Ornare/src/Ornare.Storage/SqliteStorageService.cs):

### Schema Definitions:
```sql
CREATE TABLE IF NOT EXISTS assets (
    id TEXT PRIMARY KEY,
    sha256 TEXT UNIQUE NOT NULL,
    canonical_ico_path TEXT NOT NULL,
    source_format TEXT NOT NULL,
    created_at TEXT NOT NULL,
    last_used_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS folder_assignments (
    id TEXT PRIMARY KEY,
    folder_path TEXT NOT NULL UNIQUE,
    ownership_epoch_id TEXT NOT NULL,
    icon_relative_path TEXT NOT NULL,
    status INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    volume_serial TEXT,
    file_id TEXT,
    current_asset_hash TEXT,
    conflict_state TEXT
);

CREATE TABLE IF NOT EXISTS history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    folder_path TEXT NOT NULL,
    operation_id TEXT NOT NULL,
    previous_asset_hash TEXT,
    new_asset_hash TEXT NOT NULL,
    operation_type TEXT NOT NULL,
    timestamp TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS favorites (
    asset_id TEXT PRIMARY KEY,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS settings (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS folder_baselines (...);
CREATE TABLE IF NOT EXISTS operation_journal (...);
```

### Key Architectural Invariants:
1. **No Image BLOBs**: Images are never stored in SQLite. Only canonical ICO filesystem paths and cryptographic SHA-256 hashes are stored.
2. **Library Delete Invariant (`INV-LIB-002`)**: Deleting an asset from the Library removes its entry from `assets` and deletes the canonical cache file in `%LocalAppData%\Ornare\icons\`, but **never** deletes or breaks the customized folder's independent `.ornare\folder.ico`. Verified by unit test `LibraryDeleteInvariantDoesNotAffectCustomizedFolder`.
3. **Recent Clearing Semantics (`INV-REC-001`)**: Clearing Recent clears the `history` table view representation only; it never deletes library assets, never resets folders, and never deletes folder-owned ICOs.
4. **Single Source of Truth (`INV-FAV-001`)**: Both Quick Panel and Manager share the same SQLite `favorites` and `assets` database tables. A favorite toggled in Manager is immediately visible in Quick Panel and vice versa.

---

## 4. Stable Folder Identity Architecture

Paths in Windows are mutable through renames and parent folder moves.
[`FolderIdentityService.cs`](file:///c:/Users/emir/Desktop/Ornare/src/Ornare.Windows/FolderIdentityService.cs) uses the Win32 `CreateFileW` API with `FILE_FLAG_BACKUP_SEMANTICS` and calls `GetFileInformationByHandle` to retrieve:
- **`VolumeSerialNumber`**: 32-bit volume serial identifier.
- **`FileIndexHigh` + `FileIndexLow`**: 64-bit NTFS / ReFS file identifier.

### Safety Contract:
- If file ID lookup fails (FAT32, network share, or restricted permissions), the service falls back gracefully to canonical path matching.
- Under no circumstances does the engine guess identities or mutate an unverified directory.

---

## 5. Folder Health and Safe Repair Engine

[`FolderHealthEngine.cs`](file:///c:/Users/emir/Desktop/Ornare/src/Ornare.Windows/FolderHealthEngine.cs) and [`RepairEngine.cs`](file:///c:/Users/emir/Desktop/Ornare/src/Ornare.Windows/RepairEngine.cs) guarantee non-destructive maintenance:

### Health Evaluation (Strictly Read-Only):
1. **`Healthy`**: Folder exists, has `ReadOnly` attribute, `.ornare\folder.ico` exists and is non-empty, `desktop.ini` exists with `Hidden+System` attributes and points to `.ornare\folder.ico,0`.
2. **`NeedsReconciliation` / `MissingOwnedIcon` / `ConfigMismatch`**: Missing attributes or missing folder.ico, but restorable from canonical library ICO.
3. **`ExternalIconConflict`**: `desktop.ini` points to an external resource (e.g. `shell32.dll,4` or user-defined icon).
4. **`MissingTarget`**: Folder path no longer exists on disk.

### Safe Repair Rules:
- **Zero Silent Overwrite**: When `ExternalIconConflict` is detected, `RepairEngine.RepairFolder` returns a conflict error and halts unless the user explicitly chose `forceExternalConflictResolution=true` via UI action ("Reapply").
- **Durable Mutation Reuse**: Repair operations use the atomic journal, ensuring rollback safety on any crash or power failure.

---

## 6. Architecture Gate Evaluation

| Architectural Invariant | Requirement | Status | Evidence |
|:---|:---|:---:|:---|
| **Deterministic Dedup** | SHA-256 canonical hashing across library | **VERIFIED** | Unit test `AssetDeduplicationWorksDeterministically` |
| **Library Delete Isolation** | Deleting asset preserves folder `.ornare\folder.ico` | **VERIFIED** | Integration test `LibraryDeleteInvariantDoesNotAffectCustomizedFolder` |
| **Single Favorites Truth** | Unified SQLite persistence across surfaces | **VERIFIED** | Unit test `FavoritesAndRecentHistoryAreCorrectlyManaged` |
| **Read-Only Health Check** | Zero silent mutation during scan | **VERIFIED** | Integration test `HealthEngineAccuratelyDetectsStates` |
| **Conflict Protection** | External conflict blocked from silent repair | **VERIFIED** | Integration test `RepairEngineRecoversMissingIconAndProtectsExternalConflict` |
| **Durable Mutation Reuse** | Repair uses atomic journal | **VERIFIED** | `RepairEngine.cs` calls `FolderCustomizer.ApplyIcon` |
| **Privacy Redaction** | Personal paths stripped from diagnostics | **VERIFIED** | Unit test `DiagnosticReportRedactsUserPaths` |
| **Offline Guarantee** | 0 cloud, 0 network, 0 telemetry | **VERIFIED** | Static code audit; `verify.ps1` 0 vulnerability report |

**Architectural Verdict: PASS.**
