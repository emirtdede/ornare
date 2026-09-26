# Feature Gate Report: F23 — Reset from Explorer

**Feature ID:** F23  
**Feature Name:** Reset from Explorer  
**Specification:** `feature-expansion/features/F23_reset-from-explorer.md`  
**Execution Order:** 22 of 27  
**Traceability ID:** `ORN-EXP-F23`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Completion Date:** 2026-09-26  

---

## 1. Executive Summary

Feature F23 exposes the safe Reset operation directly from the Windows 11 File Explorer context menu flyout, command line (`--reset "<folderPath>"`), and custom URI protocol (`ornare://reset?v=1&path=...`), without compromising semantic merge, ownership boundaries, or cleanup protections.

Key architectural deliverables:
1. **Windows 11 Explorer Flyout Extension (`F23-DEC-01`, `F23-DEC-03`):**
   - Extended `CLSID_OrnareFlyoutCommand` (`{A5E650D8-04B2-4A82-9B7C-295822368940}`) in native C++ (`src/Ornare.ShellExtension/dllmain.cpp`) to enumerate exactly three sub-commands in fixed deterministic order:
     1. "Apply last icon" (`OrnareApplyLastCommand`)
     2. "Undo" (`OrnareUndoCommand`)
     3. "Reset" (`OrnareResetCommand`)
   - `OrnareResetCommand` implements `IExplorerCommand`:
     - Returns `ECF_DEFAULT` (flags `0x0`).
     - Returns `E_NOTIMPL` for `EnumSubCommands` (strictly non-nested).
     - Title localized: English "Reset", Turkish "Sıfırla".
     - ToolTip localized: English "Reset folder icon to Windows default", Turkish "Klasör simgesini Windows varsayılanına sıfırla".
     - Shell extension remains a lightweight COM dispatcher executing `ornare://reset?v=1&path=<url_encoded_path>` with `--reset "<folderPath>"` CLI fallback. Zero database or filesystem logic runs in Windows Explorer.
2. **Decoupled Architecture & Layer Cleanliness (`IFolderOperationStorage`, `IResetStorage`):**
   - Introduced `IFolderOperationStorage` in `Ornare.Core` extending `IJournalStorage`, providing shared contracts for folder-level operations (`FindAssignmentAsync`, `GetLatestBaselineForAssignmentAsync`, `RemoveFolderAssignmentAsync`, `UpdateAssignmentPathAsync`, `UpdateAssignmentHealthAsync`).
   - Defined `IResetStorage` in `Ornare.Core` inheriting `IFolderOperationStorage`.
   - `IStorageService` in `Ornare.Storage` inherits `IResetStorage` without ambiguous method call conflicts (CS0121).
3. **Live Semantic Merge & Ownership Protection (`F23-DEC-04`, `INV-CUSTOMIZE-001`):**
   - Implemented `ResetService` in `Ornare.Windows`.
   - Re-reads the live `desktop.ini` immediately before commit.
   - Merges semantics via `DesktopIniParser.MergeSemanticsForReset`, stripping only Ornare-owned icon keys (`IconResource`, `IconFile`, `IconIndex`) while preserving 100% of unrelated sections and keys (`[ViewState]`, `FolderType`, `[HarmlessEnterpriseMetadata]`, `InfoTip`).
   - Never performs a blind whole-file snapshot restoration.
4. **External Conflict Protection (`F23-DEC-05`):**
   - Pre-flight inspects live folder attributes and `desktop.ini`: if external tools or the user changed the icon away from Ornare ownership, returns `ErrorCode.ExternalConflict` without clobbering foreign customizations.
5. **Physical Identity Reconciliation & Target Changed Guard (`F23-DEC-07`):**
   - Live NTFS identity (`VolumeSerialNumber`, `FileId64`) is resolved using `NtfsIdentityResolver`.
   - If the folder was moved or renamed within the same volume, the physical identity resolves the assignment and reconciles the path.
   - If the physical identity changed from the recorded assignment (e.g. folder deleted and replaced by a different directory), returns `ErrorCode.TargetChanged` and aborts.
6. **Deterministic No-Op on Non-Customized Folders (`F23-DEC-08`):**
   - If the folder is already Windows default (no Ornare customization), returns typed `ErrorCode.NotCustomized` with zero filesystem side effects.
7. **Atomic Serialization & Cleanup (`F23-DEC-06`):**
   - Per-target `SemaphoreSlim` synchronization (`ConcurrentDictionary<string, SemaphoreSlim>`) guarantees rapid consecutive or duplicate calls are safely serialized.
   - Deletes only proven Ornare-owned artifacts (`.ornare\folder.ico` and empty `.ornare` folder). Never touches non-Ornare files.
   - Cleans up folder assignment from SQLite and refreshes Windows Explorer via targeted `SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW)`.

---

## 2. Invariants & Gates Verification

| Invariant / Decision | Description | Verification Evidence | Status |
|---|---|---|---|
| `INV-APP-001` | WinUI 3 Native & Explorer Stability | Native C++ COM shell extension built for x64 with no Explorer restart or taskkill. | **PASS** |
| `INV-CUSTOMIZE-001` | Safe folder customization & live semantic merge | Verified: Unrelated `desktop.ini` keys/sections preserved byte/semantically, `.ornare\folder.ico` cleaned up. | **PASS** |
| `INV-STORAGE-002` | Durable journal before filesystem mutation | Verified: Journal record created, completed atomically on success, assignment removed on reset. | **PASS** |
| `F23-DEC-01` | Fixed Flyout Child Order (3 children) | Verified in `VerifyOrnareFlyoutCommandImplementsHasSubCommandsAndEnumeratesChildrenInOrder`: Exact order Item 0 = "Apply last icon", Item 1 = "Undo", Item 2 = "Reset". | **PASS** |
| `F23-DEC-02` | Golden path command untouched | Verified: "Set Folder Icon" remains independent top-level context menu verb (`CLSID_OrnareCommand`). | **PASS** |
| `F23-DEC-03` | Shell sends versioned command only | Verified: Shell extension executes `ornare://reset?v=1&path=...` or `--reset "<path>"`. Zero SQLite/filesystem logic in COM DLL. | **PASS** |
| `F23-DEC-04` | Live semantic merge (no whole-file restore) | Verified in `ResetAfterFolderApplyRevertsToBaselineAndPreservesUnrelatedIniContent`: `[ViewState]` and `InfoTip` preserved while icon is reset. | **PASS** |
| `F23-DEC-05` | External conflict protection | Verified in `ResetWhenExternalConflictOccurredDoesNotOverwriteExternalState`: Overwritten folder returns `ErrorCode.ExternalConflict` and preserves external state. | **PASS** |
| `F23-DEC-06` | Owned cleanup only | Verified: Only `.ornare\folder.ico` and empty `.ornare` folder deleted; non-Ornare files untouched. | **PASS** |
| `F23-DEC-07` | NTFS identity & TargetChanged check | Verified in `ResetWhenFolderRenamedResolvesByNtfsIdentityAndResetsSemantics` and `ResetWhenTargetFolderIdentityReplacedReturnsTargetChanged`. | **PASS** |
| `F23-DEC-08` | Non-customized deterministic no-op | Verified in `ResetWhenFolderNotCustomizedReturnsNotCustomizedWithoutMutation`: Returns `ErrorCode.NotCustomized` with zero mutation. | **PASS** |
| `F23-DEC-09` | Bilingual localization | Verified: English "Reset", Turkish "Sıfırla" based on OS language. | **PASS** |

---

## 3. Implementation Details

### A. Core Layer (`src/Ornare.Core/`)
- `OperationResult.cs`: Added `ErrorCode.NotCustomized` and `ErrorCode.UnsupportedTarget`.
- `IFolderOperationStorage.cs`: Common abstraction for folder-level operations:
  - `FindAssignmentAsync(NtfsFolderIdentity? identity, string canonicalPath, CancellationToken ct = default)`
  - `GetLatestBaselineForAssignmentAsync(string assignmentId)`
  - `RemoveFolderAssignmentAsync(string folderPath)`
  - `UpdateAssignmentPathAsync(string oldFolderPath, string newFolderPath)`
  - `UpdateAssignmentHealthAsync(string folderPath, FolderCustomizationState status, string? conflictState = null)`
- `IUndoStorage.cs`: Inherits `IFolderOperationStorage`.
- `IResetStorage.cs`: Inherits `IFolderOperationStorage`.

### B. Storage Layer (`src/Ornare.Storage/`)
- `IStorageService.cs`: Inherits `IResetStorage` alongside `IUndoStorage` and `IApplyLastStorage`.

### C. Windows Layer (`src/Ornare.Windows/`)
- `ResetService.cs`:
  - Implemented `ResetAsync(string folderPath, IResetStorage storage, IFolderCustomizer customizer, CancellationToken ct = default)`.
  - Serializes operations per folder path using `ConcurrentDictionary<string, SemaphoreSlim>`.
  - Validates folder via `TargetFolderValidator.ValidateTarget`.
  - Resolves live NTFS identity via `FolderIdentityService.GetNtfsIdentity`.
  - Inspects physical identity against assignment: returns `ErrorCode.TargetChanged` if volume serial or file ID changed.
  - Inspects live `desktop.ini` semantics: returns `ErrorCode.NotCustomized` if folder is already default, or `ErrorCode.ExternalConflict` if modified externally.
  - Reconciles updated paths in SQLite via `storage.UpdateAssignmentPathAsync` if renamed.
  - Records operation journal entry (`OperationKind.Reset`).
  - Calls `customizer.ResetIcon(canonicalPath, baseline)`.
  - Removes assignment and completes journal entry.
  - Refreshes shell via `ShellRefreshService.NotifyFolderChanged`.

### D. Shell Extension Layer (`src/Ornare.ShellExtension/`)
- `dllmain.cpp`:
  - Implemented `OrnareResetCommand` (`IExplorerCommand`):
    - `GetTitle`: Returns "Reset" (English) or "Sıfırla" (Turkish).
    - `GetToolTip`: Returns "Reset folder icon to Windows default" / "Klasör simgesini Windows varsayılanına sıfırla".
    - `GetFlags`: Returns `ECF_DEFAULT` (`0x0`).
    - `EnumSubCommands`: Returns `E_NOTIMPL`.
    - `Invoke`: Dispatches `ornare://reset?v=1&path=...` via `ShellExecuteW` with `--reset "<path>"` process fallback.
  - Updated `OrnareFlyoutCommand::EnumSubCommands`:
    - Enumerates 3 child commands: 1. `OrnareApplyLastCommand`, 2. `OrnareUndoCommand`, 3. `OrnareResetCommand`.

### E. Application Layer (`src/Ornare.App/`)
- `App.xaml.cs`:
  - Added CLI routing for `--reset "<folderPath>"`: runs `ResetService.ResetAsync` silently with zero UI window popups.
  - Added Protocol routing for `ornare://reset`: parses path and executes reset cleanly.

---

## 4. Test Evidence & Gate Sign-off

### Test Results
- **ResetFromExplorerIntegrationTests:** 7 of 7 passed (100% green):
  - `ResetWhenFolderNotCustomizedReturnsNotCustomizedWithoutMutation`: PASS
  - `ResetAfterFolderApplyRevertsToBaselineAndPreservesUnrelatedIniContent`: PASS
  - `ResetWhenExternalConflictOccurredDoesNotOverwriteExternalState`: PASS
  - `ResetWhenFolderRenamedResolvesByNtfsIdentityAndResetsSemantics`: PASS
  - `ResetWhenTargetFolderIdentityReplacedReturnsTargetChanged`: PASS
  - `RapidConcurrentResetCallsSerializeCleanly`: PASS
  - `ResetOnInvalidTargetRejectsWithAppropriateError`: PASS
- **ShellExtensionFlyoutTests:** 2 of 2 passed (100% green):
  - `VerifyOrnareFlyoutCommandImplementsHasSubCommandsAndEnumeratesChildrenInOrder` (all 3 children verified): PASS
  - `VerifyMenuConstructionPerformanceWithin10MsBudget`: PASS
- **ExplorerCommandStressTests:** Passed (100% green).
- **Full Solution Test Suite (`dotnet test -c Release`):**
  - All test projects green.
- **Managed ARM64 Release Build (`pwsh scripts/build-managed-arm64.ps1`):**
  - Built all 6 managed projects (`Core`, `DesignSystem`, `Windows`, `Storage`, `Imaging`, `App`) cleanly for `win-arm64` with **0 errors, 0 warnings**.
- **Golden Path Verification (`pwsh scripts/test-real-golden-path.ps1`):**
  - End-to-end Explorer and filesystem integrity validated with 0 Explorer restarts and 0 taskkill operations.

### Verification Sign-off
- **Architectural Integrity:** Verified against `00_SHARED_ARCHITECTURE_CONTRACT.md`.
- **Preflight Gate:** Passed all criteria in `production_artifacts/feature-gates/F23/PREFLIGHT.md`.
- **Quality Gates:** Invariants `INV-APP-001`, `INV-CUSTOMIZE-001`, and `INV-STORAGE-002` fully met.

Feature F23 is **COMPLETE** and verified ready for production integration.

---

## Post-Implementation Gap Closure

During the mandatory GAP-CLOSURE audit (GAP_CLOSURE_F19_F24):
- **Precision Correction**: Clarified that prior build evidence represented managed ARM64 compilation, not ARM64 Native AOT.
- **Native C++ Shell Extension Parity & Stress Testing**:
  - Rebuilt and verified `Ornare.ShellExtension.vcxproj` with MSBuild:
    - Release|x64: 0 errors, 0 warnings. AMD64 machine type, High Entropy VA, ASLR, DEP, CFG (`guardN=34`).
    - Release|ARM64: 0 errors, 0 warnings. AA64 machine type, High Entropy VA, ASLR, DEP, CFG (`guardN=34`).
  - Stress Tests: Verified `ExplorerCommandStressTests` (1,000 iterations, 0 handle leaks across all 3 flyout sub-commands).
- **Native AOT Verification**:
  - x64 Native AOT: PASS (`Ornare.App.exe` and `Ornare.Cli.exe`). PE32+ AMD64 native executables verified.
  - ARM64 Native AOT: PASS (`Ornare.App.exe` and `Ornare.Cli.exe`). PE32+ AA64 native executables verified.
  - ARM64 Physical Runtime: NOT PROVEN / PENDING HARDWARE CERTIFICATION.
- **Performance Evaluation**: Explorer Reset measured across 20 iterations: P50: 180.53 ms, P95: 216.18 ms (Budget: < 500 ms).
- **Automated Regression Suite**: 415 passed, 0 failed across entire solution.
- **Final Decision for F23**: PASS (Recertified).
