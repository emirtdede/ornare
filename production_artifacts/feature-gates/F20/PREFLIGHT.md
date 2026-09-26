# Preflight Check: F20 — Folder Drag & Drop into Manager

**Feature ID:** F20  
**Feature Name:** Folder Drag & Drop into Manager  
**Execution Order:** 19 of 27  
**Traceability ID:** `ORN-EXP-F20`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Specification:** `feature-expansion/features/F20_manager-folder-drag-drop.md`  
**Date:** 2026-09-26  

---

## 1. Scope & Objective

Allow one or many filesystem folders dragged from File Explorer into Manager to become a temporary validated working selection ("Target Tray") without blocking UI, creating false customization records or mutating any folder.

The feature subsystem must:
1. Use Windows App SDK / WinUI drag-and-drop APIs only; zero third-party drag/drop frameworks.
2. Ensure drop creates an **ephemeral Manager Target Tray / staging selection**. Drop alone creates NO persisted "Customized Folder" row, no history/journal row, and NO filesystem mutation on disk.
3. Keep existing Customized Folders records as the sole source of persisted customization state; records are created/updated only by explicit customization operations.
4. If a dropped folder identity matches a known/customized record in SQLite, the staging item must reference that existing record ID as read-only contextual metadata and must not create a duplicate staging/ownership record.
5. Closing Manager, clearing Target Tray, or starting a fresh session discards the uncommitted staging selection.
6. Maximum input per single drop is **1000 items**. If raw item count > 1000, reject the entire drop before deep validation with a clear localized limit notice.
7. Duplicates are coalesced first by F19's qualified `(VolumeSerialNumber, FileId64)` identity for eligible NTFS targets; for items rejected before identity extraction, duplicates are coalesced by canonical path.
8. Validation concurrency is fixed at **4** (`Parallel.ForEachAsync` with `MaxDegreeOfParallelism = 4`); validation is cancellable via `CancellationToken`.
9. Present rejected count with expandable/readable details; zero modal popups per item.
10. UI list is virtualized; cancellation stops remaining validation and retains items completed before cancellation, clearly marked as partial staging.

---

## 2. Invariants & Risk Mitigations

| Invariant / Risk | Specification Rule | Planned Mitigation |
|---|---|---|
| `F20-DEC-01` | WinUI 3 drag-and-drop APIs only | Use `UIElement.AllowDrop`, `DragOver`, `Drop` with `DataPackageView`. |
| `F20-DEC-02` | Ephemeral staging; zero persistence on Drop | Drop handler only populates in-memory `ObservableCollection<StagedFolderVm>`. Snapshot tests verify 0 DB/disk writes. |
| `F20-DEC-03` | Link existing customized records | Check `IStorageService.GetAssignmentByIdentityAsync` / `GetAssignmentByPathAsync` to populate `ExistingAssignmentId`. |
| `F20-DEC-04` | Discard on clear / close | `ClearStaging()` clears in-memory collection; window closing discards state. |
| `F20-DEC-05` | 1000-item maximum limit | Reject drop immediately if `rawItems.Count > 1000` before any filesystem I/O. |
| `F20-DEC-06` | Identity-first coalescing | Match `(VolumeSerialNumber, FileId64)` first; fallback to canonical path for rejected/non-NTFS items. |
| `F20-DEC-07` | Concurrency fixed at 4 | `Parallel.ForEachAsync(..., new ParallelOptions { MaxDegreeOfParallelism = 4 })`. |
| `F20-DEC-08` | No modal dialog spam | Inline summary card showing Accepted count, Rejected count, and expandable rejection details. |
| `F20-DEC-09` | Deterministic cancellation | If cancelled, completed items stay marked as partial; incomplete items omitted. |
| `F20-DEC-10` | Zero Quick Panel / Explorer impact | Code contained entirely within `Ornare.Core`, `Ornare.Windows.FolderStagingService`, and `ManagerWindow`. |

---

## 3. Test Strategy & Plan

1. **Staging Engine Unit & Integration Tests (`tests/Ornare.IntegrationTests/FolderStagingIntegrationTests.cs`):**
   - 1, 100, 1000 folders accepted path; >1000 items rejected before deep validation.
   - Mixed files, folders, non-existent paths, UNC paths, and reparse points properly accepted or rejected.
   - Duplicate path aliases and same-identity paths coalesce to single staging item.
   - Existing customized folder in SQLite is detected and links existing assignment ID without creating duplicate DB rows.
   - DB snapshot test: verify database user_version, table row counts, and disk files remain completely identical before and after drop staging.
   - Cancellation token halts processing and produces deterministic partial staging result.
2. **Performance & Concurrency Tests:**
   - Verify `MaxDegreeOfParallelism = 4` execution.
   - No blocking calls on UI thread.
3. **Build & Release Verification:**
   - Full solution `dotnet test --configuration Release` (100% green).
   - Managed ARM64 build (`pwsh -File scripts/build-managed-arm64.ps1`) 100% green.
   - Real Golden Path verification (`pwsh -File scripts/test-real-golden-path.ps1`) 100% green.
