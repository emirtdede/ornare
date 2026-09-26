# Feature Gate Report: F19 — Portable Folder Mode

**Feature ID:** F19  
**Feature Name:** Portable Folder Mode  
**Specification:** `feature-expansion/features/F19_portable-folder-mode.md`  
**Execution Order:** 18 of 27  
**Traceability ID:** `ORN-EXP-F19`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Completion Date:** 2026-09-26  

---

## 1. Executive Summary

Feature F19 delivers Portable Folder Mode, qualifying and exposing the portability inherently provided by folder-owned relative icon storage (`.ornare\folder.ico` and `desktop.ini` with `IconResource=.ornare\folder.ico,0`), backed by a deterministic identity and path-reconciliation algorithm for ordinary local fixed-drive NTFS folders without unsupported universal claims.

Key architectural deliverables:
1. **Zero User Toggle for Core Portability:** Core portability is always enabled. Every supported customization stores `.ornare\folder.ico` inside the target directory and configures `desktop.ini` using relative path syntax (`IconResource=.ornare\folder.ico,0`).
2. **Strictly Confined Supported Scope:** Publicly qualified scope is strictly **ordinary local fixed-drive NTFS folders**. Same-volume rename, move, and nested-parent moves are fully supported and automatically reconciled. Non-fixed NTFS, removable drives, FAT/FAT32/exFAT, network shares (SMB/WebDAV), cloud placeholders, and reparse points fail closed gracefully.
3. **Stable NTFS Identity (`VolumeSerialNumber`, `FileId64`):** Folder identity is captured at customization time as `(VolumeSerialNumber, FileId64)` where `FileId64 = (nFileIndexHigh << 32) | nFileIndexLow` via Win32 `GetFileInformationByHandle`. Folder path is treated as an observation, not the permanent identity.
4. **Migration V6 & Typed Compound Indexing:** Added Migration V6 (`PRAGMA user_version = 6`) adding `volume_serial_number INTEGER` (32-bit uint) and `file_id_blob BLOB` (8-byte little-endian byte array) to `folder_assignments`, indexed via composite `idx_folder_assignments_identity(volume_serial_number, file_id_blob)`. Safely migrates existing legacy hex strings.
5. **Open-by-ID Relocation & Path Reconciliation:**
   - Implemented Win32 `OpenFileById` (`FILE_ID_DESCRIPTOR.Type = FileIdType`, `FileId.QuadPart = FileId64`, `FILE_FLAG_BACKUP_SEMANTICS`) on a volume hint handle to reopen moved or renamed folders by NTFS file index.
   - Retrieves canonical DOS paths using `GetFinalPathNameByHandleW` with `char[]` buffers avoiding `StringBuilder` CA1838 warnings, normalizes `\\?\` DOS prefixes, and verifies that volume serial number and 64-bit file index match exactly.
6. **Zero Recursive Disk Scanning:** If open-by-ID or canonical path resolution fails, status transitions to `FolderCustomizationState.LocationUnknown`. The system NEVER performs costly whole-volume or directory tree scans.
7. **No Misassociation on Path Re-creation:** When an existing path opens, its live NTFS identity is checked against the assignment. If the file ID differs (e.g. original folder was moved away and a new directory was created at the old path), ownership is not misassociated; the engine tracks down the relocated original folder via open-by-ID.
8. **Decoupled Architecture:** `FolderReconciliationService` in `Ornare.Windows` operates as a pure domain evaluation engine returning `ReconciliationDecision`, preserving strict separation between Windows OS interop and SQLite storage mutations.
9. **Accurate UI Wording:** Folders list in Manager displays `"Portable on supported NTFS"` (`"Desteklenen NTFS üzerinde taşınabilir"` in Turkish) without misleading universal claims.

---

## 2. Invariants & Gates Verification

| Invariant / Decision | Description | Verification Evidence | Status |
|---|---|---|---|
| `F19-DEC-01` | No user toggle for portability; relative `.ornare\folder.ico` is mandatory | Verified: All folder customizations store icon inside `.ornare\` and use relative `desktop.ini` paths. | **PASS** |
| `F19-DEC-02` | Supported scope: ordinary local fixed-drive NTFS | Verified: `FolderIdentityService.IsSupportedFixedNtfsVolume` verifies `DRIVE_FIXED` and `NTFS` via `GetVolumeInformationW`. | **PASS** |
| `F19-DEC-03` | Fail closed on non-fixed, removable, FAT, UNC, reparse | Verified: Non-NTFS, UNC, or invalid roots immediately return `false` / `null`. | **PASS** |
| `F19-DEC-04` | Stable identity is `(VolumeSerialNumber, FileId64)` | Verified: `NtfsFolderIdentity` encapsulates 32-bit volume serial and 64-bit file index. `GetNtfsIdentityOnLiveFolderReturnsStableNonZeroIdentity` PASS. | **PASS** |
| `F19-DEC-05` | Zero whole-volume disk scanning | Verified: Unresolvable file IDs directly yield `LocationUnknown` with 0 disk traversals. | **PASS** |
| `F19-DEC-06` | Different identity at old path prevents misassociation | Verified in `EvaluateAssignmentWhenPathReplacedByNewFolderDetectsIdentityMismatchAndRelocates`. | **PASS** |
| `F19-DEC-07` | UI wording strictly `Portable on supported NTFS` | Verified: Localized text in `ManagerWindow.xaml.cs` displays exact approved phrase. | **PASS** |
| `F19-DEC-08` | Migration V6: 32-bit INTEGER and 8-byte LE BLOB with compound index | Verified in `ForwardMigrationV6AddsIdentityColumnsAndReachesVersion6` and `ForwardMigrationV5ToV6PreservesExistingAssignmentsAndMigratesHex`. | **PASS** |
| `F19-DEC-09` | No background watcher; on-demand evaluation | Verified: Zero background services or file system watchers registered; reconciliation runs on activation/load. | **PASS** |

---

## 3. Implementation Details

### A. Core Models (`src/Ornare.Core/`)
- Updated `FolderCustomizationState.cs`:
  - Added `LocationUnknown = 5` enum value representing tracked folders missing from their recorded path whose NTFS identity cannot be resolved.
- Created `NtfsFolderIdentity.cs`:
  - Readonly struct holding `uint VolumeSerialNumber` and `ulong FileId64`.
  - Helpers: `GetFileIdBytes()`, `FromBytes()`, `VolumeSerialHex`, `FileIdHex`.
- Updated `FolderAssignment.cs`:
  - Added typed `VolumeSerialNumber` (`uint?`), `FileId64` (`ulong?`), and `FileIdBlob` (`byte[]?`) properties.

### B. Windows Identity & Reconciliation Services (`src/Ornare.Windows/`)
- Implemented `FolderIdentityService.cs`:
  - Win32 P/Invokes: `CreateFileW`, `GetFileInformationByHandle`, `OpenFileById`, `GetFinalPathNameByHandleW`, `GetVolumeInformationW`, `GetDriveTypeW` with `char[]` buffers (zero CA1838 warnings).
  - `GetNtfsIdentity(string folderPath)`: extracts `(dwVolumeSerialNumber, nFileIndexHigh << 32 | nFileIndexLow)`.
  - `IsSupportedFixedNtfsVolume(string path)`: validates drive fixed type and NTFS filesystem name.
  - `TryReopenAndReconcilePath(...)`: reopens directory by 64-bit file index via volume hint handle, verifies handle identity, extracts canonical DOS path, and normalizes `\\?\` prefix.
  - `FindVolumeRootBySerialNumber(uint volumeSerialNumber)`: scans fixed drive roots to resolve volume root if hint path is missing or moved across drive mount points.
- Implemented `FolderReconciliationService.cs`:
  - `EvaluateAssignment(FolderAssignment assignment)`: pure evaluation returning `ReconciliationDecision`.
  - Handles `Healthy`, `Relocated`, `LocationUnknown`, `MissingTarget`.
  - Distinguishes path existence with identity mismatch from healthy existence.

### C. Storage Layer & Migration V6 (`src/Ornare.Storage/`)
- Updated `IStorageService.cs`:
  - Added `UpdateAssignmentIdentityAsync(string folderPath, uint? volumeSerialNumber, ulong? fileId64)`.
  - Added `GetAssignmentByIdentityAsync(uint volumeSerialNumber, ulong fileId64)`.
  - Added `GetAssignmentByPathAsync(string folderPath)`.
  - Added `UpdateAssignmentPathAsync(string oldFolderPath, string newFolderPath)`.
- Implemented Migration V6 in `SqliteStorageService.cs`:
  - Adds `volume_serial_number INTEGER` and `file_id_blob BLOB` to `folder_assignments`.
  - Introspects table schema with `PRAGMA table_info` before querying legacy columns.
  - Converts existing hex strings to typed numbers and 8-byte little-endian BLOBs.
  - Creates compound index `idx_folder_assignments_identity(volume_serial_number, file_id_blob)`.
  - Sets `PRAGMA user_version = 6;`.

### D. User Interface Integration (`src/Ornare.App/`)
- Updated `ManagerWindow.xaml` and `ManagerWindow.xaml.cs`:
  - Added `PortabilityLabel` to folder list item template displaying `"Portable on supported NTFS"`.
  - On `LoadFoldersAsync`, evaluates `FolderReconciliationService.EvaluateAssignment(a)` for missing paths.
  - Automatically migrates database records via `UpdateAssignmentPathAsync` when relocated folder is found.
  - Sets `LocationUnknown` status badge when open-by-ID fails.
- Updated `QuickPanelWindow.xaml.cs`:
  - Stores captured `NtfsFolderIdentity` (`VolumeSerialNumber`, `FileId64`) on icon apply.

---

## 4. Test & Verification Evidence

- **Storage Unit Tests (`tests/Ornare.Storage.Tests/PortableFolderStorageTests.cs`):**
  - `ForwardMigrationV6AddsIdentityColumnsAndReachesVersion6`: PASS
  - `ForwardMigrationV5ToV6PreservesExistingAssignmentsAndMigratesHex`: PASS
  - `UpdateAssignmentIdentityAndQueryByIdentityRoundtrip`: PASS
  - `UpdateAssignmentPathUpdatesPathAndPreservesIdentity`: PASS
- **Reconciliation Integration Tests (`tests/Ornare.IntegrationTests/PortableFolderReconciliationTests.cs`):**
  - `IsSupportedFixedNtfsVolumeValidatesDriveCorrectly`: PASS
  - `GetNtfsIdentityOnLiveFolderReturnsStableNonZeroIdentity`: PASS
  - `RenameAndRelocationTryReopenAndReconcilePathRecoversCanonicalPath`: PASS
  - `EvaluateAssignmentWhenFolderRelocatedReturnsRelocatedDecision`: PASS
  - `EvaluateAssignmentWhenPathReplacedByNewFolderDetectsIdentityMismatchAndRelocates`: PASS
  - `EvaluateAssignmentWhenFolderDeletedCompletelyReturnsLocationUnknownWithoutDiskScan`: PASS
  - `EvaluateAssignmentWhenAssignmentHasNoNtfsIdentityReturnsMissingTarget`: PASS
- **Entire Solution Test Suite (`dotnet test --configuration Release`):**
  - `Ornare.Core.Tests`: 64 passed (0 failed)
  - `Ornare.UiTests`: 9 passed (0 failed)
  - `Ornare.Storage.Tests`: 53 passed (0 failed)
  - `Ornare.IntegrationTests`: 56 passed (0 failed)
  - `Ornare.Imaging.Tests`: 178 passed (0 failed)
  - **TOTAL: 360 passed, 0 failed.**
- **Managed ARM64 Build (`pwsh -File scripts/build-managed-arm64.ps1`):**
  - All 6 managed projects (`Ornare.Core`, `Ornare.DesignSystem`, `Ornare.Windows`, `Ornare.Storage`, `Ornare.Imaging`, `Ornare.App`) built cleanly for `win-arm64` (0 warnings, 0 errors).
- **Real Golden Path Verification (`pwsh -File scripts/test-real-golden-path.ps1`):**
  - End-to-end Explorer-to-durable-icon flow passed with 0 Explorer restarts and 0 shell crashes.

---

## 5. Gate Sign-off

Feature F19 complies with all non-negotiable invariants, security gates, and Definition of Done criteria. All 360 solution tests and ARM64 release builds are 100% green. F19 is approved for local commit.

---

## Post-Implementation Gap Closure

During the mandatory GAP-CLOSURE audit (GAP_CLOSURE_F19_F24):
- **Precision Correction**: Clarified that earlier compilation evidence represented managed ARM64 cross-compilation (`dotnet build -r win-arm64`), which is distinct from Native AOT cross-compilation.
- **x64 Native AOT Verification**: PASS (`dotnet publish src/Ornare.App/Ornare.App.csproj -c Release -r win-x64 -p:PublishAot=true`). PE32+, machine AMD64 (8664), entry point `wmainCRTStartup`, Subsystem Windows GUI, DEP, ASLR, High Entropy VA, CFG (`guardN=131`), CET compatible.
- **ARM64 Native AOT Verification**: PASS (`dotnet publish src/Ornare.App/Ornare.App.csproj -c Release -r win-arm64 -p:PublishAot=true`). PE32+, machine AA64, entry point `wmainCRTStartup`, Subsystem Windows GUI, DEP, ASLR, High Entropy VA, CFG (`guardN=127`).
- **ARM64 Physical Runtime**: NOT PROVEN / PENDING HARDWARE CERTIFICATION (cross-compiled native binary verified at PE header level; physical runtime pending target hardware).
- **Automated Regression Suite**: 415 passed, 0 failed across entire solution.
- **Final Decision for F19**: PASS (Recertified).
