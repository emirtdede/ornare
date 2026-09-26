# Feature Gate Report: F21 — Apply Last Icon

**Feature ID:** F21  
**Feature Name:** Apply Last Icon  
**Specification:** `feature-expansion/features/F21_apply-last-icon.md`  
**Execution Order:** 20 of 27  
**Traceability ID:** `ORN-EXP-F21`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Completion Date:** 2026-09-26  

---

## 1. Executive Summary

Feature F21 implements the **"Apply last icon"** capability across Windows 11 File Explorer, CLI arguments, and custom URI protocol activations. When users repeatedly organize collections of folders, this feature enables one-click application of the most recently applied icon recipe to any eligible directory, ensuring full durability, folder portability, atomic transactional guarantees, and automatic cache rebuilding if intermediate ICO artifacts have been deleted.

Key architectural deliverables:
1. **Windows 11 Explorer Flyout Architecture (`F21-DEC-05`):**
   - Declared top-level app flyout command `CLSID_OrnareFlyoutCommand` (`{A5E650D8-04B2-4A82-9B7C-295822368940}`) in native C++ (`src/Ornare.ShellExtension/dllmain.cpp`) alongside the primary golden path verb "Set Folder Icon".
   - `OrnareFlyoutCommand` implements `IExplorerCommand` returning `ECF_HASSUBCOMMANDS` (flag `0x1`).
   - Implements `IEnumExplorerCommand` via `OrnareSubCommandEnumerator`, enumerating exactly one child command in F21: "Apply last icon" ("Son simgeyi uygula").
   - Registered as a surrogate in `Package.appxmanifest` under `windows.fileExplorerContextMenus` for `Directory` with verb `OrnareFlyout`.
2. **Resilient Cache & Recipe Rebuild (`F21-DEC-02`):**
   - Every icon render operation stores the complete `RenderRecipe` JSON snapshot inside SQLite (`render_cache.recipe_json`).
   - Calculated properties (`ImagePlacement`, `Background`) are annotated with `[JsonIgnore]` and deterministically restored upon deserialization.
   - When "Apply Last Icon" is invoked, if the physical staged ICO file has been removed or deleted, `ApplyLastService` falls back to `RebuildIcoFromRecipeAsync`, dynamically re-synthesizing the exact multi-size ICO directly from the stored JSON recipe and re-caching it.
3. **Strict Durability & Relative Semantics (`INV-CUSTOMIZE-001`):**
   - The applied folder receives a fully self-contained `.ornare\folder.ico` asset with relative `IconResource=.ornare\folder.ico,0` in `desktop.ini`.
   - The target folder remains 100% portable across drive renames and system moves without external absolute file dependencies.
4. **Decoupled Architecture & Layer Cleanliness:**
   - Introduced `IApplyLastStorage` in `Ornare.Core` with zero dependency on SQLite.
   - Implemented in `Ornare.Storage` via `SqliteStorageService` (`GetLastAppliedRenderKeyAsync`, `SetLastAppliedRenderKeyAsync`, `ClearLastAppliedRenderKeyAsync`).
   - Service logic encapsulated in `ApplyLastService` within `Ornare.Windows`.
5. **Durable Journal & Identity Capture (`INV-STORAGE-002`):**
   - Captures baseline state prior to writing `desktop.ini`.
   - Records atomic operation journal (`ApplyLastIcon`) and transitions folder assignment state to `Healthy`.
   - Captures NTFS volume serial and 64-bit file ID for tracking across renames.
   - Shell refreshed via targeted `SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW)`.
6. **Retention Immunity (`F18 Reachability Guard`):**
   - In accordance with F18, `SqliteStorageService` actively checks `settings WHERE key = 'LastAppliedRenderKey'` in `IsAssetOrRenderReferencedAsync` and `GetUnreferencedAssetsAsync`, guaranteeing that neither the cached ICO nor its underlying source asset is pruned by unreferenced asset cleanup jobs.
7. **Bilingual Support:**
   - Native Explorer verb titles and WinUI UI messages support English and Turkish dynamically based on system UI language (`GetUserDefaultUILanguage()`).

---

## 2. Invariants & Gates Verification

| Invariant / Decision | Description | Verification Evidence | Status |
|---|---|---|---|
| `INV-APP-001` | Windows 11 WinUI 3 Native & Explorer Extension | Native C++ COM shell extension built for x64 with no Explorer restart or taskkill. | **PASS** |
| `INV-CUSTOMIZE-001` | Relative icon path durability | Verified: Applied folders contain `.ornare\folder.ico` and `desktop.ini` points to `.ornare\folder.ico,0`. | **PASS** |
| `INV-STORAGE-002` | Durable journal before filesystem commit | Verified: Journal record created, baseline captured, and folder assignment recorded in SQLite. | **PASS** |
| `F21-DEC-01` | `NoLastAppliedRecipe` failure categorization | Verified in `ApplyLastWhenNoIconHasBeenAppliedFailsWithNoLastAppliedRecipe`: Returns `ErrorCode.NoLastAppliedRecipe` gracefully without crashing. | **PASS** |
| `F21-DEC-02` | Dynamic cache rebuilding on deleted ICO | Verified in `ApplyLastWhenCacheFileDeletedRebuildsFromRecipeAndApplies`: File deleted from disk, recipe rebuilt from SQLite `recipe_json`, and target folder successfully customized. | **PASS** |
| `F21-DEC-03` | Target validation & reparse point rejection | Verified in `ApplyLastOnInvalidTargetsRejectsWithAppropriateError`: Reparse points, non-existent directories, and invalid paths rejected with structured `ErrorCode`. | **PASS** |
| `F21-DEC-04` | Non-blocking shell extension latency (<10ms) | Verified in `VerifyShellExtensionGetStateAndGetTitleLatencyUnder10MsBudget`: Explorer command instantiation and state evaluation complete in <10ms. | **PASS** |
| `F21-DEC-05` | Two top-level app entries in Explorer context menu | Verified in `VerifyOrnareFlyoutCommandImplementsHasSubCommandsAndEnumeratesSingleApplyLastChild`: Native DLL exports `CLSID_OrnareFlyoutCommand` with `ECF_HASSUBCOMMANDS` (`0x1`) and single child. | **PASS** |
| `F21-DEC-06` | F18 cleanup retention of last applied key | Verified in `LastAppliedRenderKeyRoundtripAndCleanupRetention`: Settings key stored and retained through storage cleanup cycles. | **PASS** |
| `F21-DEC-07` | Dual activation paths (CLI + Protocol) | Verified: `--apply-last "<path>"` and `ornare://apply-last?v=1&path=...` wired in `App.xaml.cs`. | **PASS** |

---

## 3. Implementation Details

### A. Core Layer (`src/Ornare.Core/`)
- `OperationResult.cs`: Added `ErrorCode.NoLastAppliedRecipe` (value 24).
- `RenderRecipe.cs`:
  - Added `[JsonIgnore]` on derived properties (`ImagePlacement`, `Background`).
  - Added `ToJson()` and `FromJson(string json)` serialization methods.
- `IApplyLastStorage.cs`:
  - Created storage abstraction for `ApplyLastService`:
    - `GetLastAppliedRenderKeyAsync(CancellationToken ct = default)`
    - `SetLastAppliedRenderKeyAsync(string renderKey, CancellationToken ct = default)`
    - `GetRenderCacheEntryAsync(string renderKey, CancellationToken ct = default)`
    - `GetSourceAssetAsync(string sha256Hex, CancellationToken ct = default)`
    - `StoreRenderCacheEntryAsync(RenderCacheEntry entry, CancellationToken ct = default)`
    - `GetAssignmentByPathAsync(string folderPath, CancellationToken ct = default)`
    - `SaveBaselineAsync(FolderBaseline baseline, CancellationToken ct = default)`
    - `RecordOperationJournalAsync(Guid id, string opType, string target, string payload, CancellationToken ct = default)`
    - `MarkOperationJournalCompletedAsync(Guid id, CancellationToken ct = default)`
    - `UpsertAssignmentAsync(FolderAssignment assignment, CancellationToken ct = default)`

### B. Storage Layer (`src/Ornare.Storage/`)
- `IStorageService.cs`: Inherits `IApplyLastStorage`.
- `SqliteStorageService.cs`:
  - Implemented `GetLastAppliedRenderKeyAsync()`, `SetLastAppliedRenderKeyAsync()`, and `ClearLastAppliedRenderKeyAsync()`.
  - Stored inside `settings` table under key `"LastAppliedRenderKey"`.
  - Guarded against cleanup in `IsAssetOrRenderReferencedAsync` and `GetUnreferencedAssetsAsync`.

### C. Imaging Layer (`src/Ornare.Imaging/`)
- `ImagePipeline.cs` & `PngExportService.cs`:
  - Store serialized `recipe.ToJson()` inside `RenderCacheEntry.RecipeJson` upon rendering.

### D. Windows Layer (`src/Ornare.Windows/`)
- `ApplyLastService.cs`:
  - Implemented `ApplyLastAsync(string folderPath, IApplyLastStorage storage, IFolderCustomizer customizer, IImagePipeline? pipeline = null, CancellationToken ct = default)`.
  - Reentrant sync lock per folder path.
  - Validates folder path via `TargetFolderValidator.ValidateTarget`.
  - Retrieves `LastAppliedRenderKey`.
  - Falls back to `RebuildIcoFromRecipeAsync` if disk ICO is missing.
  - Captures prior baseline via `DesktopIniParser.ReadSemantics`.
  - Writes operation journal entry.
  - Invokes `customizer.ApplyIcon(folderPath, icoPath)`.
  - Captures NTFS identity and records `FolderAssignment` with `FolderCustomizationState.Healthy`.
  - Completes journal entry and notifies shell via `ShellNotificationService.NotifyFolderChanged`.

### E. Shell Extension Layer (`src/Ornare.ShellExtension/`)
- `dllmain.cpp`:
  - Defined `CLSID_OrnareFlyoutCommand` (`{A5E650D8-04B2-4A82-9B7C-295822368940}`).
  - Implemented `OrnareFlyoutCommand` (`IExplorerCommand`) returning `ECF_HASSUBCOMMANDS` (`0x1`) and `EnumSubCommands`.
  - Implemented `OrnareApplyLastCommand` (`IExplorerCommand`) executing `ornare://apply-last?v=1&path=...` with `--apply-last` fallback.
  - Implemented `OrnareSubCommandEnumerator` (`IEnumExplorerCommand`) enumerating single child command.
  - Registered class factory in `DllGetClassObject`.

### F. Application & Packaging Layer (`src/Ornare.Package/` & `src/Ornare.App/`)
- `Package.appxmanifest`:
  - Declared `CLSID_OrnareFlyoutCommand` in surrogate `com:Class` extension.
  - Registered desktop verb `OrnareFlyout` under `desktop4:FileExplorerContextMenus`.
- `App.xaml.cs`:
  - Added command-line handler `--apply-last "<path>"`.
  - Added protocol handler `ornare://apply-last`.
  - Added recipe rebuild fallback: `RebuildIcoFromRecipeAsync(RenderCacheEntry entry, string expectedPath)`.
- `QuickPanelWindow.xaml.cs` & `ManagerWindow.xaml.cs`:
  - Updated to save `SetLastAppliedRenderKeyAsync(entry.RenderKey)` after successful commit.

---

## 4. Test Evidence & Gate Sign-off

### Test Results
- **ApplyLastIntegrationTests:** 7 of 7 passed (100% green).
- **ShellExtensionFlyoutTests:** 1 of 1 passed (100% green).
- **AssetDeduplicationStorageTests (`LastAppliedRenderKeyRoundtripAndCleanupRetention`):** Passed (100% green).
- **Full Solution Test Suite (`dotnet test -c Release`):**
  - `Ornare.Core.Tests`: 64 passed, 0 failed
  - `Ornare.UiTests`: 9 passed, 0 failed
  - `Ornare.Storage.Tests`: 54 passed, 0 failed
  - `Ornare.IntegrationTests`: 74 passed, 0 failed
  - `Ornare.Imaging.Tests`: 178 passed, 0 failed
  - **Total: 379 passed, 0 failed (100% GREEN).**
- **Managed ARM64 Release Build (`pwsh scripts/build-managed-arm64.ps1`):**
  - Built all 6 managed projects (`Core`, `DesignSystem`, `Windows`, `Storage`, `Imaging`, `App`) cleanly for `win-arm64` with **0 errors, 0 warnings**.
- **Golden Path Verification (`pwsh scripts/test-real-golden-path.ps1`):**
  - End-to-end Explorer and filesystem integrity validated with 0 Explorer restarts and 0 taskkill operations.

### Verification Sign-off
- **Architectural Integrity:** Verified against `00_SHARED_ARCHITECTURE_CONTRACT.md`.
- **Preflight Gate:** Passed all criteria in `production_artifacts/feature-gates/F21/PREFLIGHT.md`.
- **Quality Gates:** Invariants `INV-APP-001`, `INV-CUSTOMIZE-001`, and `INV-STORAGE-002` fully met.

Feature F21 is **COMPLETE** and verified ready for production integration.
