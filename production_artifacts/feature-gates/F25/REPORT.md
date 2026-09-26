# Feature Gate Report: F25 — Operation Planning and Dry Run

**Feature ID:** F25  
**Feature Name:** Operation Planning and Dry Run  
**Specification:** `feature-expansion/features/F25_dry-run.md`  
**Execution Order:** 24 of 27  
**Traceability ID:** `ORN-EXP-F25`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Completion Date:** 2026-09-26  

---

## 1. Executive Summary

Feature F25 implements first-class, zero-mutation operation planning and `--dry-run` validation across all mutating Ornare surfaces, exposing rich operation plans in both human-readable and machine-readable JSON format (`schemaVersion = 1`).

Key architectural deliverables:
1. **Core Domain Planning Abstractions (`src/Ornare.Core/`):**
   - `ExecutionMode.cs`: Defines explicit operation execution modes (`Normal`, `DryRun`).
   - `OperationPlan.cs`: Defines domain models `PlanStatus` (`WouldSucceed`, `WouldFail`, `Indeterminate`), `PlanBlocker` (`Code`, `Message`), and `OperationPlan` (`Command`, `Target`, `Status`, `PlannedActions`, `Blockers`, `IndeterminateConditions`, `TargetIdentity`, `SourceMetadata`, `CacheExpectation`, `ExpectedArtifact`, `ExpectedArtifactSizeBytes`).
   - Added `ExecutionMode`, `PlanStatus`, `PlanBlocker`, and `OperationPlan` to `FolderJsonContext.cs` with full Native AOT source-generation annotations.
2. **Read-Only SQLite Storage Mode (`src/Ornare.Storage/`):**
   - Extended `IStorageService` with `bool IsReadOnly { get; }` and `Task InitializeReadOnlyAsync(string? dbPath = null)`.
   - Updated `SqliteStorageService`:
     - Opens SQLite connections with `SqliteOpenMode.ReadOnly` (or in-memory `:memory:` if the database does not exist), guaranteeing that zero database files, locks, journals, or WAL files are created or modified during dry run.
     - Disabled write-on-read in `GetRenderCacheEntryAsync` when in read-only mode (`if (!_isReadOnly)`), preventing unauthorized cache timestamp updates.
     - Enforced `EnsureWritable()` guards on all 23 mutating methods to prevent accidental writes.
3. **Shared Prepare/Planning Service (`src/Ornare.Windows/OperationPlanningService.cs`):**
   - Implemented `OperationPlanningService` providing unified operation preparation logic for:
     - `PlanApplyAsync`: Validates folder eligibility, target volume serial/file ID, cache presence, and expected artifact size.
     - `PlanApplyLastAsync`: Checks last-applied recipe existence and render cache entries.
     - `PlanResetAsync`: Validates current customization state and checks for third-party `desktop.ini` external conflicts.
     - `PlanUndoAsync`: Validates assignment history and baseline availability.
     - `PlanExportIcoAsync`: Validates target path, directory permissions, and expected multi-frame structure.
     - `PlanExportPngAsync`: Validates target path, directory permissions, and target dimension (256/512/1024).
4. **CLI Integration (`src/Ornare.Cli/`):**
   - Added `--dry-run` option to all 6 mutating subcommands: `apply`, `apply-last`, `reset`, `undo`, `export-ico`, and `export-png`.
   - Enforced strict rejection of `--dry-run` on `inspect` with exit code 2 (`InvalidArguments`) because `inspect` is already intrinsically read-only.
   - Enhanced `CliHandlers` to wire `--dry-run` directly to `OperationPlanningService` and read-only storage.
   - Enhanced `CliResponseFormatter.WritePlan` to emit standardized `CliEnvelope<OperationPlan>` under `--json` and human-formatted plans in text mode.
   - Registered `OperationPlan`, `PlanBlocker`, and `PlanStatus` in `CliJsonContext` for reflection-free Native AOT serialization.

---

## 2. Invariants & Gates Verification

| ID | Invariant / Requirement | Verification Method | Status |
|---|---|---|---|
| **INV-F25-01** | Zero mutations to filesystem during dry run | Automated probes check `desktop.ini`, `.ornare/`, target attributes, and export files | **PASS** |
| **INV-F25-02** | Zero mutations to SQLite storage during dry run | Automated probes check DB state, journal, and read-only connection initialization | **PASS** |
| **INV-F25-03** | Zero writes to render cache during dry run | Write-on-read guarded by `!IsReadOnly`; no new cache rows or updated timestamps | **PASS** |
| **INV-F25-04** | `inspect --dry-run` rejected with exit code 2 | Integration tests verify exit code 2 in both JSON and human text modes | **PASS** |
| **INV-F25-05** | Shared Prepare phase between DryRun and Real execution | `OperationPlanningService` shared preflight analysis matches real engine checks | **PASS** |
| **INV-F25-06** | TOCTOU revalidation & zero lock retention | Tests verify dry run holds no locks and real execution revalidates state at commit | **PASS** |
| **INV-F25-07** | Pure JSON stdout compliance under `--dry-run --json` | Single JSON document on stdout matching schemaVersion 1 | **PASS** |
| **INV-F25-08** | Multi-architecture Native AOT compilation | Clean compilation & publish for win-x64 and win-arm64 with zero warnings | **PASS** |

---

## 3. Test Evidence

### 3.1 Dedicated Integration Tests (`tests/Ornare.IntegrationTests/DryRunIntegrationTests.cs`)
1. `InspectRejectsDryRunFlagWithExitCode2`: Validates exit code 2 and pure JSON error.
2. `ApplyDryRunZeroMutationVerification`: Validates `desktop.ini`, `.ornare/`, and folder attributes remain untouched.
3. `ApplyLastDryRunReportsBlockerWhenNoPreviousApply`: Validates exit code 4 (`Conflict`) and blocker `NoLastAppliedRecipe`.
4. `ApplyLastDryRunWouldSucceedAfterRealApply`: Validates status `WouldSucceed` and cache hit report.
5. `ResetDryRunZeroMutationOnUncustomizedAndCustomizedFolders`: Validates blocker on uncustomized, and zero-mutation preservation on customized folder.
6. `UndoDryRunZeroMutationOnUncustomizedAndCustomizedFolders`: Validates blocker on uncustomized, and zero-mutation preservation on customized folder.
7. `ExportIcoDryRunZeroMutationVerification`: Validates zero files written to disk for ICO export.
8. `ExportPngDryRunZeroMutationVerification`: Validates zero files written to disk for PNG export.
9. `BlockerVerificationForInvalidInputsAndTargets`: Validates blockers for missing target, missing source, corrupt image, bad PNG dimensions, bad ICO extension.
10. `ToctouRevalidationAndLockIndependence`: Validates lock release after dry run and revalidation upon real commit.

### 3.2 Full Regression Test Suite Results
- `Ornare.Core.Tests`: 64 tests passed (0 failed, 0 skipped)
- `Ornare.Imaging.Tests`: 178 tests passed (0 failed, 0 skipped)
- `Ornare.Storage.Tests`: 54 tests passed (0 failed, 0 skipped)
- `Ornare.UiTests`: 9 tests passed (0 failed, 0 skipped)
- `Ornare.IntegrationTests`: 120 tests passed (0 failed, 0 skipped)
- **Total: 425 tests passed across all 5 test suites.**

---

## 4. Multi-Architecture Build & Native AOT Status

- **x64 Release & Native AOT:**
  - `Ornare.Cli.exe`: Published cleanly to `win-x64\publish\` (0 warnings, 0 errors).
  - `Ornare.App.exe`: Built cleanly for `x64` (0 warnings, 0 errors).
- **ARM64 Release & Native AOT:**
  - `Ornare.Cli.exe`: Cross-compiled and published cleanly to `win-arm64\publish\` (0 warnings, 0 errors).
  - `Ornare.App.exe`: Built cleanly for `arm64` (0 warnings, 0 errors).

---

## 5. Exit Criteria & Readiness for F26

All exit criteria defined in `feature-expansion/features/F25_dry-run.md` and `00_SHARED_ARCHITECTURE_CONTRACT.md` are satisfied. F25 is certified **COMPLETE**. Progression to F26 (Keyboard-First Workflow) is approved.
