# Preflight Check: F13 — ICO Export

**Feature ID:** F13  
**Feature Name:** ICO Export  
**Execution Order:** 13 of 27  
**Traceability ID:** `ORN-EXP-F13`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Specification:** `feature-expansion/features/F13_ico-export.md`  
**Date:** 2026-09-26  

---

## 1. Scope & Objective

Export any selected asset or rendered recipe as a verified, high-quality multi-resolution Windows icon (`.ico`) containing exactly the 10 qualified Ornare frame sizes: `16, 20, 24, 32, 40, 48, 64, 96, 128, 256`.
The export pipeline must:
1. Maximize performance by copying from the F18 render cache on exact cache hits.
2. Guarantee strict atomicity on the physical destination using Windows native `ReplaceFileW` and `MoveFileExW(MOVEFILE_WRITE_THROUGH)` via temporary sibling files (`.ornare-export-{GUID}.tmp`).
3. Ensure zero corrupt or partially overwritten files upon cancellation, access denied, full disk, or injected failure.
4. Prevent pollution of the internal asset library with user export paths.

---

## 2. Invariants & Security Boundaries

1. **Exact Frame Compliance (Fixed Decision 1):**
   - Output `.ico` contains all 10 standard frames: `16, 20, 24, 32, 40, 48, 64, 96, 128, 256`.
2. **Cache Reuse (Fixed Decision 2):**
   - If verified cache entry exists for `RenderKey`, copy bytes directly; otherwise render once, validate, cache, and export.
3. **SaveFilePicker & Zero Cancel Mutation (Fixed Decision 3):**
   - `SaveFilePicker` initialized with window HWND is the only GUI destination chooser.
   - User cancel results in 0 file writes, 0 temporary files, and 0 database changes.
4. **Atomic Sibling Temporary File (Fixed Decision 4 & 6):**
   - Temporary file created in destination directory with name `.ornare-export-{GUID}.tmp` using `FileMode.CreateNew`.
   - Flushed to physical disk (`FileStream.Flush(flushToDisk: true)`).
   - Validated for valid ICO container and 10 frames before commit.
   - If destination exists: `ReplaceFileW` replaces atomically without truncate.
   - If destination does not exist: `MoveFileExW` with `MOVEFILE_WRITE_THROUGH` (0x8).
   - If the provider does not support atomic replace/move, fail with typed `ErrorCode.UnsupportedExportDestination`.
   - On any failure or exception, only the sibling temp is deleted; pre-existing destination is untouched.
5. **Sibling Temp Cleanup Rule:**
   - Before creating a new temp, scan destination directory only for `.ornare-export-*.tmp` files older than 24 hours and attempt deletion.
6. **Library Identity Isolation (Fixed Decision 6):**
   - Exported paths are never added to `LibraryAsset` or SQLite tables.

---

## 3. Test Strategy & Plan

- Unit & integration tests in `tests/Ornare.Windows.Tests` or `tests/Ornare.Imaging.Tests`:
  - `ExportIco_RoundTrip_VerifiesAllTenFrames`: Export then re-import verifies all 10 frames bit-exact.
  - `ExportIco_ExistingDestination_ReplacedAtomically`: Verifies existing file is atomically replaced.
  - `ExportIco_InjectedFailure_PreservesOriginalDestination`: Failure during temp generation or validation leaves original destination byte-for-byte identical.
  - `ExportIco_CacheHit_MatchesCacheArtifactByteForByte`: Exact cache hit copies bytes without re-encoding.
  - `ExportIco_Cancel_CreatesNoFiles`: Canceling picker creates no files.
  - `ExportIco_StaleTempCleanup`: Deletes > 24h `.ornare-export-*.tmp` and ignores recent or in-use files.
- Full solution build and test pass.
- ARM64 managed build green.
- Real golden path pass.
