# Feature Gate Report: F22 — Undo from Explorer

**Feature ID:** F22  
**Feature Name:** Undo from Explorer  
**Specification:** `feature-expansion/features/F22_undo-from-explorer.md`  
**Execution Order:** 21 of 27  
**Traceability ID:** `ORN-EXP-F22`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Completion Date:** 2026-09-26  

---

## 1. Executive Summary

Feature F22 exposes the existing safe Ornare Undo operation directly from the Windows 11 File Explorer context menu flyout, command-line interface, and protocol activation, without moving history, database, or filesystem logic into the native shell extension or Windows Explorer process.

Key architectural deliverables:
1. **Windows 11 Explorer Flyout Extension (`F22-DEC-01`, `F22-DEC-05`):**
   - Extended `CLSID_OrnareFlyoutCommand` (`{A5E650D8-04B2-4A82-9B7C-295822368940}`) in native C++ (`src/Ornare.ShellExtension/dllmain.cpp`) to enumerate exactly two sub-commands in fixed deterministic order:
     1. "Apply last icon" (`OrnareApplyLastCommand`)
     2. "Undo" (`OrnareUndoCommand`)
   - `OrnareUndoCommand` implements `IExplorerCommand` returning `ECF_DEFAULT` (flags `0x0`) and `EnumSubCommands` returning `E_NOTIMPL` (no nested flyouts).
   - Shell extension remains lightweight, native, and fail-safe, dispatching activation either via `ornare://undo?v=1&path=<url_encoded_path>` or `--undo "<folderPath>"` CLI fallback.
2. **Decoupled Architecture & Shared Interface (`IJournalStorage`):**
   - Introduced `IJournalStorage` in `Ornare.Core` defining common journal and baseline contracts (`RecordJournalEntryAsync`, `UpdateJournalStateAsync`, `RecordHistoryAsync`, `GetLatestBaselineAsync`), eliminating ambiguous interface inheritance conflicts (CS0121).
   - Defined `IUndoStorage` inheriting `IJournalStorage`, specifying folder assignment queries, health tracking, and rename reconciliations (`FindAssignmentAsync`, `GetLatestBaselineForAssignmentAsync`, `RemoveFolderAssignmentAsync`, `UpdateAssignmentPathAsync`, `UpdateAssignmentHealthAsync`).
   - Implemented in `Ornare.Storage` via `SqliteStorageService`.
3. **Safe Reversion & Atomic Serialization (`F22-DEC-04`, `F22-DEC-06`):**
   - Implemented `UndoService` in `Ornare.Windows` with per-target `SemaphoreSlim` synchronization (`ConcurrentDictionary<string, SemaphoreSlim>`), ensuring rapid consecutive or concurrent undo calls are deterministically serialized.
   - If no assignment or baseline exists, returns `ErrorCode.NoUndoAvailable` (error code 29) with zero disk side effects.
   - Performs pre-flight baseline conflict check against live folder attributes and `desktop.ini`: if external tools or the user modified the icon semantics away from Ornare's ownership, returns `ErrorCode.ExternalConflict` (17) without clobbering external customizations.
4. **NTFS Volume & File ID Rename Resilience (`F22-DEC-07`):**
   - Inspects target NTFS volume serial number and 64-bit file index via Windows APIs (`NtfsIdentityResolver`).
   - If the folder was renamed or moved within the volume, resolves the new physical path, updates SQLite records, and executes a clean undo.
5. **Durable Journaling & Shell Notification (`INV-STORAGE-002`, `INV-CUSTOMIZE-001`):**
   - Creates a durable journal entry (`JournalOperationType.UndoCustomization`) prior to filesystem mutation.
   - Reverts `desktop.ini` cleanly to prior baseline semantics, removes `.ornare\folder.ico` and `.ornare` metadata folder if empty, and removes the folder assignment record from SQLite.
   - Refreshes Windows Explorer using targeted `SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW)`.
6. **Bilingual Localization (`F22-DEC-08`):**
   - Context menu title dynamically localized: English "Undo", Turkish "Geri Al" via `GetUserDefaultUILanguage()`.
   - Tooltips dynamically localized: English "Revert to previous folder icon", Turkish "Önceki klasör simgesine geri dön".

---

## 2. Invariants & Gates Verification

| Invariant / Decision | Description | Verification Evidence | Status |
|---|---|---|---|
| `INV-APP-001` | WinUI 3 Native & Shell Extension Safety | Native C++ COM shell extension built for x64 with no Explorer restart or taskkill. | **PASS** |
| `INV-CUSTOMIZE-001` | Safe folder customization & baseline restoration | Verified: Baseline semantics cleanly restored, `.ornare` artifacts unlinked, non-Ornare desktop.ini sections preserved. | **PASS** |
| `INV-STORAGE-002` | Durable journal before filesystem mutation | Verified: Journal record created, completed atomically on success, assignment removed on undo. | **PASS** |
| `F22-DEC-01` | Fixed Flyout Child Order | Verified in `VerifyOrnareFlyoutCommandImplementsHasSubCommandsAndEnumeratesChildrenInOrder`: Exact order Item 0 = "Apply last icon", Item 1 = "Undo". | **PASS** |
| `F22-DEC-02` | Golden path command untouched | Verified: "Set Folder Icon" remains independent top-level context menu verb (`CLSID_OrnareCommand`). | **PASS** |
| `F22-DEC-03` | Shell sends versioned command only | Verified: Shell extension executes `ornare://undo?v=1&path=...` or `--undo "<path>"`. Zero SQLite/filesystem logic in COM DLL. | **PASS** |
| `F22-DEC-04` | External conflict prevention | Verified in `UndoWhenFolderExternallyModifiedReturnsExternalConflictWithoutOverwrite`: Overwritten folder returns `ErrorCode.ExternalConflict` and preserves external state. | **PASS** |
| `F22-DEC-05` | No nested sub-commands | Verified in `VerifyOrnareFlyoutCommandImplementsHasSubCommandsAndEnumeratesChildrenInOrder`: `OrnareUndoCommand::EnumSubCommands` returns `E_NOTIMPL`. | **PASS** |
| `F22-DEC-06` | Rapid duplicate serialization | Verified in `RapidConcurrentUndoCallsSerializeCleanly`: First call reverts, second call safely returns `ErrorCode.NoUndoAvailable` with 0 corruption. | **PASS** |
| `F22-DEC-07` | NTFS rename path reconciliation | Verified in `UndoOnRenamedFolderReconcilesNtfsIdentityAndRestoresBaseline`: Renamed folder resolved via FileId64/VolumeSerial and restored. | **PASS** |
| `F22-DEC-08` | Bilingual context menu titles | Verified: English "Undo", Turkish "Geri Al" based on OS language. | **PASS** |

---

## 3. Implementation Details

### A. Core Layer (`src/Ornare.Core/`)
- `OperationResult.cs`: Added `ErrorCode.NoUndoAvailable` (value 29) and `ErrorCode.TargetChanged` (value 30).
- `IJournalStorage.cs`: Common abstraction for journal and baseline operations:
  - `RecordJournalEntryAsync(JournalEntry entry, CancellationToken ct = default)`
  - `UpdateJournalStateAsync(Guid journalId, JournalState state, string? error = null, CancellationToken ct = default)`
  - `RecordHistoryAsync(FolderHistoryEntry entry, CancellationToken ct = default)`
  - `GetLatestBaselineAsync(Guid assignmentId, CancellationToken ct = default)`
- `IApplyLastStorage.cs`: Inherits `IJournalStorage`, deduplicating shared methods.
- `IUndoStorage.cs`: Inherits `IJournalStorage`, defining:
  - `FindAssignmentAsync(string folderPath, CancellationToken ct = default)`
  - `GetLatestBaselineForAssignmentAsync(Guid assignmentId, CancellationToken ct = default)`
  - `RemoveFolderAssignmentAsync(Guid assignmentId, CancellationToken ct = default)`
  - `UpdateAssignmentPathAsync(Guid assignmentId, string newPath, CancellationToken ct = default)`
  - `UpdateAssignmentHealthAsync(Guid assignmentId, FolderHealthState health, CancellationToken ct = default)`

### B. Storage Layer (`src/Ornare.Storage/`)
- `IStorageService.cs`: Inherits `IUndoStorage` alongside `IApplyLastStorage`.
- `SqliteStorageService.cs`:
  - Implemented `GetLatestBaselineForAssignmentAsync(Guid assignmentId, CancellationToken ct = default)`.
  - Updated `UpdateAssignmentIdentityAsync` to parse volume serial numbers and file IDs with fallback to `NumberStyles.Integer` for decimal/hex compatibility.

### C. Windows Layer (`src/Ornare.Windows/`)
- `UndoService.cs`:
  - Implemented `UndoAsync(string folderPath, IUndoStorage storage, IFolderCustomizer customizer, CancellationToken ct = default)`.
  - Serializes operations per folder path using `ConcurrentDictionary<string, SemaphoreSlim>`.
  - Validates folder via `TargetFolderValidator.ValidateTarget`.
  - Resolves NTFS identity across renames/moves using `NtfsIdentityResolver.TryGetIdentity`.
  - Reconciles updated paths in SQLite via `storage.UpdateAssignmentPathAsync`.
  - Performs pre-flight baseline comparison to detect external modifications (`ErrorCode.ExternalConflict`).
  - Records operation journal entry (`JournalOperationType.UndoCustomization`).
  - Calls `customizer.UndoIcon(resolvedPath, baseline)`.
  - Removes assignment and completes journal entry.
  - Refreshes shell via `ShellNotificationService.NotifyFolderChanged`.

### D. Shell Extension Layer (`src/Ornare.ShellExtension/`)
- `dllmain.cpp`:
  - Implemented `OrnareUndoCommand` (`IExplorerCommand`):
    - `GetTitle`: Returns "Undo" (English) or "Geri Al" (Turkish).
    - `GetToolTip`: Returns "Revert to previous folder icon" / "Önceki klasör simgesine geri dön".
    - `GetFlags`: Returns `ECF_DEFAULT` (`0x0`).
    - `EnumSubCommands`: Returns `E_NOTIMPL`.
    - `Invoke`: Dispatches `ornare://undo?v=1&path=...` via `ShellExecuteW` with `--undo "<path>"` process fallback.
  - Updated `OrnareFlyoutCommand::EnumSubCommands`:
    - Enumerates 2 child commands: 1. `OrnareApplyLastCommand`, 2. `OrnareUndoCommand`.

### E. Application Layer (`src/Ornare.App/`)
- `App.xaml.cs`:
  - Added CLI routing for `--undo "<folderPath>"`: runs `UndoService.UndoAsync` silently with zero UI window popups.
  - Added Protocol routing for `ornare://undo`: parses path and executes undo cleanly.

---

## 4. Test Evidence & Gate Sign-off

### Test Results
- **UndoFromExplorerIntegrationTests:** 6 of 6 passed (100% green):
  - `UndoWhenNoHistoryAvailableReturnsNoUndoAvailableWithoutMutation`: PASS
  - `UndoWhenAssignmentExistsRestoresBaselineAndRemovesAssignment`: PASS
  - `UndoWhenFolderExternallyModifiedReturnsExternalConflictWithoutOverwrite`: PASS
  - `UndoOnRenamedFolderReconcilesNtfsIdentityAndRestoresBaseline`: PASS
  - `RapidConcurrentUndoCallsSerializeCleanly`: PASS
  - `UndoOnInvalidTargetRejectsWithAppropriateError`: PASS
- **ShellExtensionFlyoutTests:** 2 of 2 passed (100% green):
  - `VerifyOrnareFlyoutCommandImplementsHasSubCommandsAndEnumeratesChildrenInOrder`: PASS
  - `VerifyShellExtensionGetStateAndGetTitleLatencyUnder10MsBudget`: PASS
- **Full Solution Test Suite (`dotnet test -c Release`):**
  - `Ornare.Core.Tests`: 64 passed, 0 failed
  - `Ornare.UiTests`: 9 passed, 0 failed
  - `Ornare.Storage.Tests`: 54 passed, 0 failed
  - `Ornare.IntegrationTests`: 80 passed, 0 failed
  - `Ornare.Imaging.Tests`: 178 passed, 0 failed
  - **Total: 385 passed, 0 failed (100% GREEN).**
- **Managed ARM64 Release Build (`pwsh scripts/build-managed-arm64.ps1`):**
  - Built all 6 managed projects (`Core`, `DesignSystem`, `Windows`, `Storage`, `Imaging`, `App`) cleanly for `win-arm64` with **0 errors, 0 warnings**.
- **Golden Path Verification (`pwsh scripts/test-real-golden-path.ps1`):**
  - End-to-end Explorer and filesystem integrity validated with 0 Explorer restarts and 0 taskkill operations.

### Verification Sign-off
- **Architectural Integrity:** Verified against `00_SHARED_ARCHITECTURE_CONTRACT.md`.
- **Preflight Gate:** Passed all criteria in `production_artifacts/feature-gates/F22/PREFLIGHT.md`.
- **Quality Gates:** Invariants `INV-APP-001`, `INV-CUSTOMIZE-001`, and `INV-STORAGE-002` fully met.

Feature F22 is **COMPLETE** and verified ready for production integration.

---

## Post-Implementation Gap Closure

During the mandatory GAP-CLOSURE audit (GAP_CLOSURE_F19_F24):
- **Precision Correction**: Clarified that prior build evidence represented managed ARM64 compilation, not ARM64 Native AOT.
- **Native C++ Shell Extension Parity**:
  - Rebuilt and verified `Ornare.ShellExtension.vcxproj` with MSBuild:
    - Release|x64: 0 errors, 0 warnings. AMD64 machine type, High Entropy VA, ASLR, DEP, CFG (`guardN=34`).
    - Release|ARM64: 0 errors, 0 warnings. AA64 machine type, High Entropy VA, ASLR, DEP, CFG (`guardN=34`).
  - Stress Tests: Explorer sub-command enumeration for `OrnareUndoCommand` verified under stress testing (1,000 iterations, 0 handle leaks).
- **Native AOT Verification**:
  - x64 Native AOT: PASS (`Ornare.App.exe` and `Ornare.Cli.exe`). PE32+ AMD64 native executables verified.
  - ARM64 Native AOT: PASS (`Ornare.App.exe` and `Ornare.Cli.exe`). PE32+ AA64 native executables verified.
  - ARM64 Physical Runtime: NOT PROVEN / PENDING HARDWARE CERTIFICATION.
- **Performance Evaluation**: Explorer Undo measured across 20 iterations: P50: 209.56 ms, P95: 245.41 ms (Budget: < 500 ms).
- **Automated Regression Suite**: 415 passed, 0 failed across entire solution.
- **Final Decision for F22**: PASS (Recertified).
