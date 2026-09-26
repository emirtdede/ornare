# Feature Gate Report: F24 — CLI and Machine-Readable JSON Output

**Feature ID:** F24  
**Feature Name:** CLI and Machine-Readable JSON Output  
**Specification:** `feature-expansion/features/F24_cli-json.md`  
**Execution Order:** 23 of 27  
**Traceability ID:** `ORN-EXP-F24`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Completion Date:** 2026-09-26  

---

## 1. Executive Summary

Feature F24 provides a robust, native AOT-friendly command-line interface (`Ornare.Cli.exe`) exposing all core folder customization, inspection, and asset export capabilities to developers, power users, and automation scripts. The CLI is exposed to the Windows Terminal and command prompt via the MSIX `uap5:AppExecutionAlias` `ornare.exe`.

Key architectural deliverables:
1. **Isolated Headless CLI Assembly (`src/Ornare.Cli/Ornare.Cli.csproj`):**
   - Independent console application (`OutputType=Exe`) targeting `net10.0-windows10.0.26100.0`.
   - References strictly domain and infrastructure libraries: `Ornare.Core`, `Ornare.Imaging`, `Ornare.Storage`, `Ornare.Windows`, and `System.CommandLine` (pinned to 2.0.0 in `Directory.Packages.props`).
   - Zero dependencies on `Ornare.App`, `Microsoft.WindowsAppSDK.WinUI`, or XAML runtime components. Can run headlessly in CI/CD pipelines, remote PowerShell, and terminal environments.
2. **Seven Authoritative CLI Subcommands:**
   - `apply <folder>`: Orchestrates durable folder icon application via `--color`, `--preset`, or `--source`.
   - `apply-last <folder>`: Re-applies the most recently configured icon styling to a folder.
   - `undo <folder>`: Reverts folder customization to the immediate previous state.
   - `reset <folder>`: Resets folder customization to Windows default and cleans up owned assets.
   - `inspect <folder>`: Non-mutating inspection returning live customization state, desktop.ini status, and identity.
   - `export-ico --output <file>`: Exports a complete multi-frame ICO file directly from render inputs.
   - `export-png --output <file> --size <256|512|1024>`: Exports a high-resolution PNG file directly from vector or raster inputs.
3. **Strict Machine-Readable Output (`--json`):**
   - Global `--json` switch supported across all subcommands (`Recursive=true`).
   - Output purity guarantee: In `--json` mode, exactly one JSON document is written to `stdout`. All informational messages are suppressed.
   - Standardized `CliEnvelope<T>` schema (version 1) with `schemaVersion`, `success`, `command`, `operationId`, `data`, `code`, and `messageKey`.
   - Human-readable mode outputs clean English summaries to `stdout` and error descriptions to `stderr`.
4. **Deterministic Exit Codes (`CliExitCodes`):**
   - `0`: Success.
   - `2`: `InvalidArguments` (syntax error, conflicting render options, unsupported arguments, invalid hex color, invalid size).
   - `3`: `TargetNotFound` (target folder or source file does not exist).
   - `4`: `TargetLocked` (exclusive lock acquisition failure).
   - `5`: `ConflictOrChanged` (external modifications detected or folder identity changed).
   - `6`: `StorageError` (underlying filesystem or SQLite I/O failure).
   - `10`: `InternalError` (unhandled exception).
5. **Render Input Mutex & Modifier Validation Matrix (`RenderInputValidator`):**
   - Exactly one input selector allowed (`--color`, `--preset`, `--source`). Supplying zero or multiple returns exit code 2 (`InvalidArguments`).
   - `--color` permits `--template` only; rejects `--mode`, `--padding`, `--recolor`.
   - `--preset` rejects all modifiers (`--template`, `--mode`, `--padding`, `--recolor`).
   - `--source` permits `--mode`, `--padding`, `--background`, `--background-color`, and `--recolor` (only with SVG). Rejects `--template`.
6. **Decoupled Architecture & Shared Core Services:**
   - Introduced `ApplyService` in `Ornare.Windows` implementing unified, journaled folder icon application.
   - Introduced `InspectService` in `Ornare.Windows` implementing non-mutating folder customization inspection.
   - Extracted `IFolderBaselineLookup`, `IApplyStorage`, and `IInspectStorage` in `Ornare.Core` to eliminate duplicate method signature ambiguities (CS0121).
   - Unified PNG export in `CliHandlers` via `PngExportService.RenderPng`.
7. **MSIX AppExecutionAlias Integration:**
   - Configured `<uap5:AppExecutionAlias>` in `Package.appxmanifest` mapping executable `Ornare.Cli.exe` to alias `ornare.exe`.
   - Added project reference in `Ornare.Package.wapproj` ensuring CLI binary is packaged in all release bundles.
   - Updated `scripts/build-managed-arm64.ps1` to include `Ornare.Cli.csproj`.

---

## 2. Invariants & Gates Verification

| Invariant / Decision | Description | Verification Evidence | Status |
|---|---|---|---|
| `INV-APP-001` | WinUI 3 Native & Explorer Stability | Verified: `Ornare.Cli.exe` runs headlessly without loading WinUI; 0 Explorer restarts or taskkills. | **PASS** |
| `INV-CUSTOMIZE-001` | Safe folder customization & live semantic merge | Verified: Full CLI lifecycle (`apply` -> `undo` -> `apply-last` -> `reset`) preserves unrelated desktop.ini content. | **PASS** |
| `INV-STORAGE-002` | Durable journal before filesystem mutation | Verified: `ApplyService` records `JournalEntry` and history in SQLite before file modifications. | **PASS** |
| `F24-DEC-01` | Native AOT-friendly CLI exposing 7 commands | Implemented in `src/Ornare.Cli/Program.cs` and `CliHandlers.cs`; verified in integration tests. | **PASS** |
| `F24-DEC-02` | Strict `--json` stdout purity | Verified in `CliExecutionIntegrationTests`: Valid JSON parsed from stdout, stderr empty, schemaVersion 1. | **PASS** |
| `F24-DEC-03` | Fixed Authoritative Exit Codes (0, 2, 3, 4, 5, 6, 10) | Verified in `CliExitCodes.cs`, `CliGrammarValidationTests`, and `CliExecutionIntegrationTests`. | **PASS** |
| `F24-DEC-04` | Render Input Mutex & Modifier Matrix | Verified in `RenderInputValidator.cs` and `CliGrammarValidationTests` (17 distinct validation cases). | **PASS** |
| `F24-DEC-05` | Non-mutating `inspect` command | Verified in `InspectService.cs` and `CliExecutionIntegrationTests`: 0 filesystem writes. | **PASS** |
| `F24-DEC-06` | Shared Core services & zero logic duplication | Reuses `ApplyService`, `ApplyLastService`, `UndoService`, `ResetService`, `InspectService`, `PngExportService`. | **PASS** |
| `F24-DEC-07` | MSIX AppExecutionAlias (`ornare.exe`) | Verified in `Package.appxmanifest` and `Ornare.Package.wapproj`. | **PASS** |
| `F24-DEC-08` | Zero App / WinUI runtime dependencies | Verified: `Ornare.Cli.csproj` references only `Core`, `Imaging`, `Storage`, `Windows`, and `System.CommandLine`. | **PASS** |
| `F24-DEC-09` | Managed ARM64 build clean | Verified via `scripts/build-managed-arm64.ps1`: All 7 managed projects compiled for `win-arm64` (0 errors, 0 warnings). | **PASS** |

---

## 3. Test & Verification Summary

### Comprehensive Test Suite Execution:
- **Ornare.IntegrationTests:** 110 passed, 0 failed, 0 skipped.
  - Grammar validation: Empty arguments, missing targets, conflicting render selectors, invalid modifiers, illegal hex formats, invalid templates, out-of-range paddings, unsupported recolor options.
  - Execution lifecycle: Full end-to-end `inspect` -> `apply` -> `inspect` -> `undo` -> `inspect` -> `apply-last` -> `inspect` -> `reset` -> `inspect` flow.
  - Export validation: Pure JSON stdout output for `export-ico` and `export-png` across 256, 512, and 1024 resolutions.
  - Headless execution: Verifying stdout/stderr separation without loading WinUI runtime.
- **Ornare.Imaging.Tests:** 178 passed, 0 failed.
- **Ornare.Core.Tests:** 64 passed, 0 failed.
- **Ornare.Storage.Tests:** 54 passed, 0 failed.
- **Grand Total:** 406 automated tests passed (0 failures).

### Golden Path End-to-End Verification:
- Executed `scripts/test-real-golden-path.ps1`:
  - Successfully customized folder with desktop.ini and relative `.ornare\folder.ico`.
  - Preserved existing `[ViewState]` and `[HarmlessEnterpriseMetadata]` sections.
  - Successfully refreshed shell without Explorer restart or taskkill.

### Managed ARM64 Cross-Compilation:
- Executed `scripts/build-managed-arm64.ps1`:
  - `Ornare.Core` (Release, win-arm64): 0 errors, 0 warnings.
  - `Ornare.DesignSystem` (Release, win-arm64): 0 errors, 0 warnings.
  - `Ornare.Windows` (Release, win-arm64): 0 errors, 0 warnings.
  - `Ornare.Storage` (Release, win-arm64): 0 errors, 0 warnings.
  - `Ornare.Imaging` (Release, win-arm64): 0 errors, 0 warnings.
  - `Ornare.Cli` (Release, win-arm64): 0 errors, 0 warnings.
  - `Ornare.App` (Release, win-arm64): 0 errors, 0 warnings.

---

## 4. Conclusion

Feature F24 is fully implemented, verified, and complete in accordance with `feature-expansion/features/F24_cli-json.md` and all parent architectural invariants.

---

## Post-Implementation Gap Closure

During the mandatory GAP-CLOSURE audit (GAP_CLOSURE_F19_F24):
- **Precision Correction**: Clarified that prior build evidence represented managed ARM64 compilation, not ARM64 Native AOT.
- **Installed MSIX AppExecutionAlias Verification (Phase G)**:
  - Rebuilt and signed fresh x64 package candidate `production_artifacts/release/candidate/Ornare_1.0.0.0_x64.msix` containing `Ornare.Cli.exe` with `AppExecutionAlias` `ornare.exe` (`desktop4:Subsystem="console"`).
  - Installed locally via `Add-AppxPackage`.
  - Executed full lifecycle from an ordinary terminal without source-tree PATH:
    1. `ornare --help`: exit code 0, complete human documentation.
    2. `ornare export-ico`: pure JSON stdout, 419,110 byte multi-resolution ICO generated.
    3. `ornare export-png`: pure JSON stdout, 1024x1024 PNG generated.
    4. Safe folder lifecycle: `inspect` -> `apply` -> `inspect` (verified `IconResource=.ornare\folder.ico,0`) -> `undo` -> `inspect` -> `apply-last` -> `reset` -> `inspect`.
    5. Alias lifecycle: Verified `Remove-AppxPackage` cleanly removes alias from `%LOCALAPPDATA%\Microsoft\WindowsApps\ornare.exe`, and reinstall restores it.
- **Package & Payload Audit (Phase H)**:
  - Validated `production_artifacts/feature-gates/GAP_CLOSURE_F19_F24/package/PACKAGE_AUDIT.md`.
  - Confirmed 0 PDBs, 0 source files, 0 test files, correct architecture PE headers across x64 and ARM64.
- **WACK & DPI Awareness (Phase I)**:
  - Executed Windows App Certification Kit (`appcert.exe`).
  - Added `SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)` in `Ornare.Cli/Program.cs` ensuring full DPI compliance.
- **Native AOT Verification**:
  - x64 Native AOT: PASS (`Ornare.Cli.exe`). PE32+ AMD64 native executable verified.
  - ARM64 Native AOT: PASS (`Ornare.Cli.exe`). PE32+ AA64 native executable verified.
  - ARM64 Physical Runtime: NOT PROVEN / PENDING HARDWARE CERTIFICATION.
- **Performance Evaluation**:
  - CLI Startup (`ornare --help`): P50: 105.98 ms, P95: 117.12 ms (Budget: < 250 ms).
  - CLI Inspect: P50: 171.73 ms, P95: 200.04 ms (Budget: < 300 ms).
  - CLI Apply: P50: 253.13 ms, P95: 311.59 ms (Budget: < 1000 ms).
- **Automated Regression Suite**: 415 passed, 0 failed across entire solution.
- **Final Decision for F24**: PASS (Recertified).
