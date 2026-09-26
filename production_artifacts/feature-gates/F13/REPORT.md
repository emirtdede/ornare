# Feature Gate Report: F13 — ICO Export

**Feature ID:** F13  
**Feature Name:** ICO Export  
**Specification:** `feature-expansion/features/F13_ico-export.md`  
**Execution Order:** 13 of 27  
**Traceability ID:** `ORN-EXP-F13`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Completion Date:** 2026-09-26  

---

## 1. Executive Summary

Feature F13 introduces a durable, atomic Windows icon export pipeline to Ornare. The exporter guarantees that exported `.ico` files contain all 10 standard Ornare frame sizes (`16, 20, 24, 32, 40, 48, 64, 96, 128, 256`).

Key architectural guarantees:
1. **Physical Atomicity & Non-Destructive Semantics:** Rather than streaming directly or truncating destination files in place, `AtomicExportWriter` creates a uniquely named sibling temporary file (`.ornare-export-{GUID}.tmp`) in the target directory using `FileMode.CreateNew`. The file is written, flushed to physical disk (`Flush(flushToDisk: true)`), and verified for container integrity and 10 frames before commit.
2. **Native Win32 Commit:**
   - Existing destinations are replaced via `ReplaceFileW(destination, temp, null, 0, ...)`.
   - New destinations are published via `MoveFileExW(temp, destination, MOVEFILE_WRITE_THROUGH)`.
   - If the destination filesystem cannot provide atomic replace/move semantics, export aborts with typed `ErrorCode.UnsupportedExportDestination` while preserving any existing file completely intact.
3. **Render Cache Reuse:** If the F18 render cache contains a verified cache artifact for the target render recipe, the export pipeline copies the verified cached bytes directly without redundant re-rendering.
4. **Library Identity Isolation:** Exported user paths are strictly external outputs; they are never inserted into SQLite `LibraryAsset` tables or internal index structures.
5. **Stale Sibling Temp Cleanup:** Prior to creating a sibling temp, the destination directory is scoped for `.ornare-export-*.tmp` files older than 24 hours, safely deleting abandoned crash artifacts while skipping files currently in use.
6. **Manager UI Integration:** Added "Export Icon (.ico)..." context action to Library items in `ManagerWindow` using `FileSavePicker` initialized with the app's native window handle (`HWND`).

---

## 2. Invariants & Gates Verification

| Invariant / Decision | Description | Verification Evidence | Status |
|---|---|---|---|
| `F13-DEC-01` | Exported ICO contains exactly the 10 qualified Ornare frames: `16, 20, 24, 32, 40, 48, 64, 96, 128, 256` | Verified in `ExportIcoRoundTripVerifiesAllTenFrames`. | **PASS** |
| `F13-DEC-02` | Exact cache hit copies verified bytes without re-encoding; otherwise renders once, validates, caches, then exports | Verified in `ExportIcoCacheHitMatchesCacheArtifactByteForByte`. | **PASS** |
| `F13-DEC-03` | `FileSavePicker` initialized with HWND is the GUI destination chooser. User cancel performs zero writes | Verified in `ManagerWindow.xaml.cs` (returns early on null file) and test suites. | **PASS** |
| `F13-DEC-04` | Sibling temporary file (`.ornare-export-{GUID}.tmp`), flushed to disk, validated, committed with `ReplaceFileW` / `MoveFileExW` | Verified in `AtomicExportWriter.cs` and `ExportIcoExistingDestinationReplacedAtomically`. | **PASS** |
| `F13-DEC-05` | Injected failure leaves pre-existing destination file 100% byte-for-byte intact with zero temp leakage | Verified in `ExportIcoInjectedFailurePreservesOriginalDestination`. | **PASS** |
| `F13-DEC-06` | Exported user paths are not canonical Library identities and are never inserted into SQLite asset tables | Verified: no database insert occurs during export. | **PASS** |
| `F13-DEC-07` | Cleanup of `.ornare-export-*.tmp` files older than 24h in destination directory; ignores active files | Verified in `ExportIcoCleanupStaleSiblingTemps`. | **PASS** |

---

## 3. Implementation Details

### A. Core Models (`src/Ornare.Core/`)
- Created `ExportIcoModels.cs`:
  - `ExportIcoRequest`: `SourceIcoPath`, `DestinationPath`, `RenderKey`.
  - `ExportIcoResult`: `DestinationPath`, `FileSizeBytes`, `FrameCount`, `IsFromCache`.
- Updated `OperationResult.cs`: Added `UnsupportedExportDestination` error code.

### B. Windows Native Exporter (`src/Ornare.Windows/`)
- Created `AtomicExportWriter.cs`:
  - `CleanupStaleSiblingTemps`: Cleans stale `.ornare-export-*.tmp` files $> 24\text{h}$ old.
  - `ValidateIcoBasic`: Fast container header and frame count validation.
  - `ExportAtomically`: Coordinates sibling temp generation, disk flush, pre-commit validation, `ReplaceFileW` / `MoveFileExW` commit, post-commit validation, and exception safety.

### C. Manager UI Integration (`src/Ornare.App/`)
- Updated `ManagerWindow.xaml`: Added `ContextExportIcoText` `MenuFlyoutItem` to `LibraryGridView.ItemTemplate`.
- Updated `ManagerWindow.xaml.cs`:
  - Added `ContextExportIcoText` to `LibraryItemVm` and localized in `CreateLibraryVm`.
  - Implemented `OnExportIcoClicked`: Opens HWND-initialized `FileSavePicker` with `.ico` filter, invokes `AtomicExportWriter.ExportAtomically`.

---

## 4. Test & Verification Evidence

- **Integration Test Suite (`tests/Ornare.IntegrationTests/AtomicExportIntegrationTests.cs`):**
  - `ExportIcoRoundTripVerifiesAllTenFrames`: PASS
  - `ExportIcoExistingDestinationReplacedAtomically`: PASS
  - `ExportIcoInjectedFailurePreservesOriginalDestination`: PASS
  - `ExportIcoCleanupStaleSiblingTemps`: PASS
  - `ExportIcoCacheHitMatchesCacheArtifactByteForByte`: PASS
- **Total Solution Tests:** All 273 tests PASS.
- **ARM64 Managed Build (`build-managed-arm64.ps1`):** PASS (0 warnings, 0 errors).
- **Real End-to-End Golden Path (`test-real-golden-path.ps1`):** PASS.

---

## 5. Traceability Compliance

- Requirement `ORN-EXP-F13` registered in `docs/REQUIREMENTS_TRACEABILITY.md`.
- Zero placeholders; zero script-generated code; zero breaking changes.
- Ready for local commit and progression to Feature 14 (F14 — PNG Export).
