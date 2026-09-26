# Feature Gate Report: F20 — Folder Drag & Drop into Manager

**Feature ID:** F20  
**Feature Name:** Folder Drag & Drop into Manager  
**Specification:** `feature-expansion/features/F20_folder-drag-and-drop-into-manager.md`  
**Execution Order:** 19 of 27  
**Traceability ID:** `ORN-EXP-F20`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Completion Date:** 2026-09-26  

---

## 1. Executive Summary

Feature F20 introduces folder drag-and-drop staging into the Manager Window, providing an interactive, high-performance Target Tray interface where users can drag folders directly from File Explorer into Ornare without mutating disk or database storage.

Key architectural deliverables:
1. **Zero Mutation Invariant on Drop (`INV-DD-001`):**
   - Dropping folders into Manager is an ephemeral staging action.
   - It performs **zero SQLite writes** (no new folder assignment, no journal entry, no history record, no settings change) and **zero filesystem writes** (`desktop.ini` is never created/altered, `.ornare` folder is never created).
   - In-memory staging state is held in an `ObservableCollection` in the Manager Window and can be cleared at any time via "Clear Staging" or window closure.
2. **Strict Drop Limit Enforcement (`F20-DEC-05`):**
   - Hard upper bound of 1,000 items per drop operation.
   - If `rawPaths.Count > 1000`, the entire drop operation is rejected immediately before running any per-item Win32 filesystem calls or database queries, protecting the UI thread and process memory.
3. **Decoupled Architecture & Layer Boundaries:**
   - `FolderStagingService` resides in `Ornare.Windows` with zero dependency on `Ornare.Storage`.
   - `IFolderAssignmentLookup` is defined in `Ornare.Core`, implemented by `SqliteStorageService`, and accepted as an optional interface by `FolderStagingService`.
   - Detects existing assignments by checking `(VolumeSerialNumber, FileId64)` first on NTFS, falling back to canonical path, attaching `ExistingAssignmentId` as read-only contextual metadata without creating duplicate DB rows.
4. **Identity Coalescence & Path Deduplication (`F20-DEC-07`):**
   - Redundant paths, case variants (`C:\Folder` vs `c:\folder`), trailing-slash variants (`C:\Folder\`), and multiple paths resolving to the same NTFS file identity coalesce into a single staged folder entry.
5. **Off-UI Execution with Bounded Concurrency (`F20-DEC-06`):**
   - Path resolution, directory existence, reparse point checks, and identity lookups run off the UI thread via `Task.Run` and `Parallel.ForEachAsync` with strictly bounded concurrency (`MaxDegreeOfParallelism = 4`).
6. **Robust Failure Categorization:**
   - Distinguishes files from folders with new `ErrorCode.ItemIsFileNotFolder`.
   - Reports non-existent paths (`ErrorCode.TargetNotFound`), reparse points (`ErrorCode.TargetIsReparsePoint`), and UNC/network paths (`ErrorCode.TargetNotLocal`).
7. **Premium WinUI 3 Target Tray Experience:**
   - Integrated Target Tray with drop landing border (`TargetTrayDropZone`), progress indicator (`ProgressRing`), virtualized staged items list (`ListView` with container recycling), rejection count badge/flyout, and "Clear Staging" action.
   - Full Turkish and English localization.

---

## 2. Invariants & Gates Verification

| Invariant / Decision | Description | Verification Evidence | Status |
|---|---|---|---|
| `INV-DD-001` | Zero persistence on drop; no DB or FS mutations | Verified in `StageFoldersZeroMutationInvariantDatabaseAndDiskRemainCompletelyIdentical`: SQLite table counts, schema `user_version`, and disk contents remain 100% identical before and after drop. | **PASS** |
| `F20-DEC-01` | Pure in-memory staging state | Verified: Staged items held in UI `ObservableCollection<StagedFolderItemVm>`; cleared on demand. | **PASS** |
| `F20-DEC-02` | Target Tray UI integration | Verified: `TargetTrayDropZone`, `TargetTrayStagedPanel`, `StagedFoldersListView`, `RejectionsListPanel` implemented in `ManagerWindow.xaml`. | **PASS** |
| `F20-DEC-03` | File vs folder discrimination | Verified in `StageFoldersItemIsFileNotFolderRejectedWithItemIsFileNotFolder`: Files rejected with `ErrorCode.ItemIsFileNotFolder`. | **PASS** |
| `F20-DEC-04` | Existing assignment linking | Verified in `StageFoldersExistingCustomizedFolderInSqliteAttachesExistingAssignmentMetadata`: Attaches `ExistingAssignmentId` with 0 duplicate DB records. | **PASS** |
| `F20-DEC-05` | 1,000 items hard limit | Verified in `StageFoldersOneThousandFoldersAcceptedAtBoundary` (1000 accepted) and `StageFoldersMoreThanOneThousandItemsRejectedImmediatelyWithoutFilesystemIo` (1001 rejected with `DropLimitExceeded` and zero I/O). | **PASS** |
| `F20-DEC-06` | Bounded concurrency (max 4) | Verified: `FolderStagingService.MaxValidationConcurrency = 4` in `ParallelOptions`. | **PASS** |
| `F20-DEC-07` | Identity & alias deduplication | Verified in `StageFoldersDuplicatePathAliasesCoalescesToSingleStagedItem`: 4 aliases coalesce into 1 accepted item. | **PASS** |
| `F20-DEC-08` | Graceful cancellation handling | Verified in `StageFoldersCancellationHaltsCleanlyAndProducesPartialResult`: CancellationToken halts cleanly and sets `IsCancelled = true`. | **PASS** |
| `F20-DEC-09` | Bilingual localization | Verified: Turkish and English string resources in `ManagerWindow.xaml.cs`. | **PASS** |

---

## 3. Implementation Details

### A. Core Layer (`src/Ornare.Core/`)
- Updated `OperationResult.cs`:
  - Added `ErrorCode.DropLimitExceeded` (value 22).
  - Added `ErrorCode.ItemIsFileNotFolder` (value 23).
- Created `FolderStagingModels.cs`:
  - `StagedFolderItem`: Represents an individual processed path (input path, canonical path, validity, rejection reason, `ErrorCode`, `NtfsFolderIdentity`, `ExistingAssignmentId`, `FolderName`, `IsAlreadyCustomized`).
  - `FolderStagingResult`: Represents overall staging outcome (`AcceptedItems`, `RejectedItems`, `TotalRawCount`, `IsLimitExceeded`, `IsCancelled`, `AcceptedCount`, `RejectedCount`).
- Created `IFolderAssignmentLookup.cs`:
  - Decoupled interface defining `Task<FolderAssignment?> FindAssignmentAsync(NtfsFolderIdentity? identity, string canonicalPath, CancellationToken ct = default);`.

### B. Storage Layer (`src/Ornare.Storage/`)
- Updated `IStorageService.cs`:
  - Inherits `IFolderAssignmentLookup`.
- Updated `SqliteStorageService.cs`:
  - Implemented `FindAssignmentAsync(NtfsFolderIdentity? identity, string canonicalPath, CancellationToken ct = default)`.
  - Queries `GetAssignmentByIdentityAsync` first if identity has non-zero values on NTFS; falls back to `GetAssignmentByPathAsync(canonicalPath)`.

### C. Windows Layer (`src/Ornare.Windows/`)
- Implemented `FolderStagingService.cs`:
  - `StageFoldersAsync`: Validates input count against `MaxDropItems = 1000`.
  - Runs parallel validation with `MaxDegreeOfParallelism = 4`.
  - Inspects file attributes to detect files vs directories (`ItemIsFileNotFolder`).
  - Invokes `TargetFolderValidator.ValidateTarget` for path security and bounds.
  - Queries `FolderIdentityService.GetNtfsIdentity` for NTFS targets.
  - Performs lookup against `IFolderAssignmentLookup` to attach existing customization metadata.
  - Coalesces duplicates using composite identity `(VolumeSerialNumber, FileId64)` for NTFS folders and canonical path for non-NTFS targets.

### D. User Interface Layer (`src/Ornare.App/`)
- Updated `ManagerWindow.xaml`:
  - Enabled `AllowDrop="True"` on `FoldersView` with `DragOver="OnFoldersViewDragOver"` and `Drop="OnFoldersViewDrop"`.
  - Added `TargetTrayDropZone` (visual landing drop target with folder glyph and guidance subtitle).
  - Added `TargetTrayStagedPanel` containing:
    - Summary header with `StagedCountText` ("3 folders staged").
    - `StagingProgressRing` for background validation indicator.
    - `ToggleRejectionsButton` showing count of rejected items with caution color.
    - `ClearStagingButton` to discard staged folders.
    - `StagedFoldersListView` displaying staged items with folder icon, name, canonical path, and status badges (`"Customized"`, `"Portable on supported NTFS"`).
    - `RejectionsListPanel` collapsible list showing rejected paths and specific rejection reasons.
- Updated `ManagerWindow.xaml.cs`:
  - Added `StagedFolderItemVm` and `RejectedFolderItemVm` view models.
  - Added `_stagedFolders` and `_rejectedFolders` observable collections.
  - Implemented drag-over filtering (`DataView.Contains(StandardDataFormats.StorageItems)`).
  - Implemented async drop handler with progress feedback and error reporting.

---

## 4. Test & Verification Evidence

- **Integration Tests (`tests/Ornare.IntegrationTests/FolderStagingIntegrationTests.cs`):**
  - `StageFoldersSingleValidFolderAcceptedWithIdentity`: PASS
  - `StageFoldersOneHundredFoldersAllAccepted`: PASS
  - `StageFoldersOneThousandFoldersAcceptedAtBoundary`: PASS
  - `StageFoldersMoreThanOneThousandItemsRejectedImmediatelyWithoutFilesystemIo`: PASS
  - `StageFoldersItemIsFileNotFolderRejectedWithItemIsFileNotFolder`: PASS
  - `StageFoldersNonExistentPathRejectedWithTargetNotFound`: PASS
  - `StageFoldersDuplicatePathAliasesCoalescesToSingleStagedItem`: PASS
  - `StageFoldersExistingCustomizedFolderInSqliteAttachesExistingAssignmentMetadata`: PASS
  - `StageFoldersZeroMutationInvariantDatabaseAndDiskRemainCompletelyIdentical`: PASS
  - `StageFoldersCancellationHaltsCleanlyAndProducesPartialResult`: PASS
- **Full Solution Verification:**
  - `dotnet test --configuration Release`: All test suites green (Core, Storage, Imaging, UI, Integration: 66/66 integration tests passed).
  - `pwsh -File scripts/build-managed-arm64.ps1`: All 6 managed projects built cleanly for ARM64 (0 errors, 0 warnings).
  - `pwsh -File scripts/test-real-golden-path.ps1`: End-to-end golden path verification completed with 0 errors.

---

## 5. Traceability & Sign-Off

- **Traceability ID:** `ORN-EXP-F20`
- **Execution Order:** 19 of 27
- **Dependencies Met:** F18, F19 complete.
- **Safety Gate:** Verified zero mutation on drop.
- **Sign-off Status:** **APPROVED / READY FOR PRODUCTION MERGE**
