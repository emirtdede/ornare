# Quality Gate Report — Gate 1 (Architecture Vertical Slice Runtime Proof)

- Gate: Gate 1 — Architecture Vertical Slice
- Date: 2026-09-25
- Evaluator: Antigravity Autonomous Agent (Principal Engineer / QA Lead Perspective)
- Host Environment: Windows 11 Pro 25H2 (Build 10.0.26200.0, x64)
- Status: **NOT PROVEN**

---

## A. Package Installation

**Status**: **NOT PROVEN**

- **MSIX Package Artifact**: `production_artifacts/release/Ornare_1.0.0.0_x64.msix` (40.78 MB).
- **Packaging Evidence**: Assembled layout directory with `makeappx.exe` (Windows SDK 10.0.26100.0) including `Ornare.App.exe`, `Ornare.ShellExtension.dll`, `AppxManifest.xml`, PRI files, and visual logo assets (`StoreLogo.png`, `Square150x150Logo.png`, `Square44x44Logo.png`).
- **Signing Evidence**: Signed with local development certificate (`CN=Ornare`, Thumbprint: `54D6998855C4FFAC09B8F043F2A6AE0A2FD7FDF4`) via `signtool.exe` (ExitCode: 0).
- **Runtime Installation Findings**:
  - `Add-AppxPackage -Path ...` failed with `HRESULT 0x800B0109`: Root certificate is not in `Cert:\LocalMachine\Root` / `Cert:\LocalMachine\TrustedPeople`. Writing to `LocalMachine` requires local Administrator elevation, which is prohibited by Ornare Non-negotiable product rules ("No hidden administrator requirement").
  - `Add-AppxPackage -Register ...\AppxManifest.xml` failed with `HRESULT 0x80073CFF`: Windows Developer Mode and Enterprise Sideloading policies are disabled in the host OS settings.
  - **Verdict**: Unpackaged execution works; OS-level package installation on this host environment cannot be completed without Developer Mode or elevation.

---

## B. Modern Explorer Context Menu

**Status**: **NOT PROVEN**

- **Native C++ COM DLL**: `src/Ornare.ShellExtension/x64/Release/Ornare.ShellExtension.dll` (38 KB).
- **Code Hardening**: Compiled with `/W4 /WX /sdl /GS /guard:cf /std:c++20 /DYNAMICBASE /NXCOMPAT /HIGHENTROPYVA`.
- **Interface**: Implements `IExplorerCommand` and `IClassFactory` for CLSID `{4B1F8E1C-6C4D-4E10-9F1C-9E29DA21A77E}`.
- **Microbenchmark**: `GetState` executes in 0.01 ms; `Invoke` executes in 0.00 ms.
- **Runtime Findings**:
  - Windows 11 modern context menu integration requires valid package identity registration (`windows.fileExplorerContextMenus`).
  - Because package registration is blocked by OS developer mode policy (`0x80073CFF`), real-world context menu appearance and user click in Windows File Explorer could not be captured on this host.
  - **Verdict**: Implementation is complete; live Explorer appearance remains `NOT PROVEN`.

---

## C. Shell → App Activation

**Status**: **VERIFIED**

- **Protocol & CLI Parsing**: Supports both `ornare://set-folder?v=1&path=...` and CLI `--set-folder "<path>"`.
- **Single-Instance Lifecycle**: Implemented with Windows App SDK `AppInstance.FindOrRegisterForKey("OrnareSingleInstance")` and `RedirectActivationToAsync`.
- **Empirical Test Evidence**:
  - Instance 1 launched with folder `src` (PID: 29972, Alive).
  - Instance 2 launched with folder `tests` (PID: 29340).
  - Instance 2 redirected activation to Instance 1 and immediately terminated (`HasExited=True`, `ExitCode=-1`).
  - Total running processes after invocation: strictly 1.
  - Activation routing latency: 412 ms.

---

## D. Real Quick Panel Rendering

**Status**: **VERIFIED**

- **Window Host**: Native Win32 window (Handle: 1509450, Title: "Ornare", 420 × 330 effective px).
- **Material**: WinUI 3 continuous Mica surface without card-in-card or panel nesting.
- **Empirical Screenshot Captures**: 8 actual runtime screenshots captured from the running process using Win32 `PrintWindow(PW_RENDERFULLCONTENT)` into `production_artifacts/design/runtime-screenshots/`:
  1. `runtime-quick-panel-light-idle.png`
  2. `runtime-quick-panel-light-drag-over.png`
  3. `runtime-quick-panel-light-success.png`
  4. `runtime-quick-panel-dark-idle.png`
  5. `runtime-quick-panel-dark-success.png`
  6. `runtime-quick-panel-turkish.png`
  7. `runtime-quick-panel-error-state.png`
  8. `runtime-quick-panel-undo-state.png`

---

## E. Actual Image → Icon Workflow

**Status**: **VERIFIED**

- **Formats Tested**: Real PNG, JPEG, and WebP decodes via Magick.NET security sandbox.
- **Contain Normalization**: Verified aspect ratio preservation, transparent square canvas centering, alpha preservation, and EXIF auto-orient.
- **10-Frame Multi-Resolution ICO**: Verified all standard Windows frames generated (16, 20, 24, 32, 40, 48, 64, 96, 128, 256 px) directly from master source (`INV-IMG-003`). Verified by `ImagePipelineTests`.

---

## F. Explorer Refresh

**Status**: **VERIFIED**

- **Mechanism**: Targeted shell notification via `SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW, path, NULL)`.
- **Integrity Guarantee**: Zero `taskkill explorer.exe`, zero Explorer process restarts, zero global icon cache deletions (`INV-EXP-001`). Verified in `FolderCustomizationIntegrationTests`.

---

## G. Undo / Reset

**Status**: **VERIFIED**

- **Undo**:
  - When applying an icon over an existing Ornare customization, `ApplyIcon` stores a hidden system backup in `.ornare\folder.prev.ico`.
  - `UndoIcon` restores `.ornare\folder.prev.ico` without disturbing external edits.
  - Verified by `FolderCustomizationIntegrationTests.UndoRestoresPreviousIconSemantics`.
- **Reset**:
  - Restores the baseline prior to Ornare's initial touch.
  - Removes `.ornare\folder.ico` and `.ornare\folder.prev.ico`.
  - Reverts folder `ReadOnly` attribute if introduced by Ornare.
  - Verified by `FolderCustomizationIntegrationTests.ResetRestoresPreOrnareSemanticsAndCleansUpOwnedAssets`.

---

## H. desktop.ini Preservation

**Status**: **VERIFIED**

- **Semantic Merge Engine**: `DesktopIniParser` selectively modifies only Ornare's owned keys (`IconResource`, `IconFile`, `IconIndex`).
- **Empirical Test Evidence**:
  - Pre-populated test folder with `[ViewState]`, `FolderType=Generic`, `[HarmlessEnterpriseMetadata]`, `DeptId=Engineering-42`, and `InfoTip=Initial Unrelated Description`.
  - Applied custom icon and re-read live file: all unrelated sections and keys were 100% preserved.
  - Verified by `ApplyPreservesUnrelatedDesktopIniContent` and `scripts/test-real-golden-path.ps1`.

---

## I. External Conflict

**Status**: **VERIFIED**

- **Conflict Detection**: When `desktop.ini` is modified externally after Ornare customizes it, `GetFolderState` returns `FolderCustomizationState.ExternalIconConflict`.
- **Overwrite Protection**: `ResetIcon` and `UndoIcon` refuse to execute silently over external changes, returning `ErrorCode.ExternalConflict`.
- **Empirical Test Evidence**: Verified by `FolderCustomizationIntegrationTests.ExternalConflictIsDetectedAndPreventsSilentReset`.

---

## J. Crash Recovery

### Simulated Fault Injection: **VERIFIED**
- Tested 5 simulated crash stages in `FaultInjectionAllCrashStagesReconcileDeterministically`:
  1. Crash at `AssetGenerated` -> Rolled back cleanly.
  2. Crash at `FilesystemStaged` -> Rolled back cleanly.
  3. Crash at `FilesystemCommitted` -> Marked completed.
  4. Crash at `ShellRefreshRequested` -> Marked completed.
  5. Crash at `MetadataCommitted` -> Marked completed.
  6. Idempotency re-run -> 0 remaining operations.

### Real Process Crash Recovery: **VERIFIED**
- Tested in `FaultInjectionCrashRecoveryTests.RealProcessCrashAndStartupReconciliation`:
  - An incomplete in-flight operation was staged in the live SQLite journal database simulating an unhandled process termination.
  - The actual `Ornare.App.exe` was launched.
  - Startup reconciliation (`ReconcileStartupAsync`) automatically processed the abandoned journal entry, resolving it to a stable state (`RolledBack`).

---

## K. Performance

### Microbenchmarks (Method Execution Time)
| Metric | Measured P50 | Measured P95 | Target Budget | Classification | Status |
|---|---|---|---|---|---|
| Shell `GetState` function body | 0.01 ms | 0.01 ms | < 10.0 ms | MICROBENCHMARK | **PASS** |
| Shell `Invoke` function body | 0.00 ms | 0.00 ms | < 10.0 ms | MICROBENCHMARK | **PASS** |
| Image Decode Latency (256x256 PNG) | 2.92 ms | 3.26 ms | < 15.0 ms | MICROBENCHMARK | **PASS** |
| 10-Frame ICO Generation | 210.11 ms | 220.19 ms | < 300.0 ms | MICROBENCHMARK | **PASS** |
| Drop to filesystem commit | 214.99 ms | 225.63 ms | < 500.0 ms | MICROBENCHMARK | **PASS** |

### Runtime E2E (Real Operating System Measurements)
| Scenario | Measured Latency | Target Budget | Classification | Status |
|---|---|---|---|---|
| Cold Process Launch to Quick Panel Visible | 2,059 ms | < 2,500 ms | RUNTIME E2E | **PASS** |
| Warm Activation Routing (Instance 2 -> Instance 1) | 412 ms | < 500 ms | RUNTIME E2E | **PASS** |
| Explorer Context Click to Quick Panel Visible | — | < 300 ms | RUNTIME E2E | **NOT PROVEN** |

### Memory Breakdown (Measured on running `Ornare.App.exe`)
- **Process**: `Ornare.App.exe` (PID: 33092)
- **State**: Idle (Quick Panel rendered on screen)
- **Private Working Set (Private Bytes)**: **71.07 MB** (Target: < 100 MB) -> **PASS**
- **Total Working Set (including shared OS/DirectX pages)**: **136.04 MB** (Target: < 150 MB) -> **PASS**
- **Peak Working Set**: **136.06 MB**

---

## L. Security

Empirical verification scope based on actual test suites:

| Category | Empirical Evidence | Status |
|---|---|---|
| Dependency Vulnerabilities | `dotnet list package --vulnerable`: 0 vulnerabilities reported. | **VERIFIED** |
| Static Analysis | WarningsAsErrors enabled; MSVC `/W4 /WX /sdl`: 0 warnings/errors. | **VERIFIED** |
| Malformed Input Tests | Corrupted and truncated image streams rejected with `ErrorCode.ImageCorrupted`. | **VERIFIED** |
| Path Traversal Validation | `..\..\` and UNC paths rejected by `TargetFolderValidator`. | **VERIFIED** |
| Reparse Point Validation | Symlinks and junctions rejected before I/O in shell and app. | **VERIFIED** |
| Native COM Boundary | `noexcept` boundaries, `HRESULT` returns, null pointer checks. | **VERIFIED** |
| Race-Condition Tests | `PerTargetSyncLock` prevents concurrent mutations on the same folder. | **VERIFIED** |
| TOCTOU Resistance | Two-tier path re-validation under per-target sync lock. | **VERIFIED** |
| Process Crash Recovery | Interrupted transactions reconciled deterministically on restart. | **VERIFIED** |
| Cross-Network / SMB Reparse Races | Network paths rejected; enterprise domain controller races not tested. | **NOT PROVEN** |
| Kernel / Memory Injection Defense | Compiler mitigations active; external driver attacks out of scope. | **NOT PROVEN** |

---

## M. UI

- **Rubric Score**: **19 / 20** (`docs/UI_REVIEW_RUBRIC.md`).
- **Evaluation Source**: Evaluated strictly on the 8 real application runtime screenshots in `production_artifacts/design/runtime-screenshots/`.
- **Anti-Pattern Audit**: Zero card-in-card nesting, zero panel-in-panel boxing, zero decorative gradients, zero emoji.
- **Reference Mockups**: Reclassified as target reference concepts in `production_artifacts/design/references/`.

---

## N. x64 Architecture

- **Managed Projects Compile**: **VERIFIED** (0 warnings, 0 errors).
- **Native C++ Shell Extension Compile**: **VERIFIED** (MSVC x64, `/W4 /WX /sdl /guard:cf`).
- **Runtime Execution**: **VERIFIED** (All 35 unit and integration tests passing on Windows 11 Pro 25H2 x64).

---

## O. ARM64 Architecture

- **Managed Projects Compile**: **VERIFIED** (`net10.0-windows10.0.26100.0` compiles cleanly for ARM64).
- **Native C++ Shell Extension Compile**: **NOT PROVEN** (Host VS BuildTools installation lacks component `Microsoft.VisualStudio.Component.VC.Tools.ARM64`).
- **Physical ARM64 Runtime Execution**: **NOT PROVEN** (Host is x64 hardware; qualification deferred to Gate 5 on physical ARM64 device).

---

## P. WACK (Windows App Certification Kit)

**Status**: **NOT PROVEN**

- Tool location: `C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe`.
- Outcome: Execution requires package deployment to Windows AppX subsystem; blocked on this host due to un-elevated environment and developer mode policy (`0x80073CFF`).

---

## Q. Remaining Risks

| Risk ID | Severity | Description | Remediation / Next Action |
|---|---|---|---|
| RSK-001 | P1 | Modern Windows 11 Context Menu package registration requires Developer Mode or root certificate trust. | Requires enabling Windows Developer Mode on the host or installing the test certificate in `Cert:\LocalMachine\TrustedPeople` via elevated prompt, or packaging for Windows Store. |
| RSK-002 | P1 | Native ARM64 MSVC compiler missing on host VS BuildTools. | Install `Microsoft.VisualStudio.Component.VC.Tools.ARM64` on build agents. |
| RSK-003 | P2 | Physical ARM64 device runtime qualification. | Deferred to Gate 5 on supported physical ARM64 hardware. |

---

## R. Gate 1 Final Decision

**NOT PROVEN**

### Rationale:
In strict accordance with the non-negotiable contract:
- **Specification ≠ Verification**: No specification is treated as runtime evidence.
- **Compiled Code ≠ Working Integration**: Compiling `Ornare.ShellExtension.dll` does not prove File Explorer context menu integration.
- **Unit Test ≠ OS Integration**: Unit tests pass (35/35), but modern Explorer context menu registration requires package identity deployment which cannot be installed on this host without Developer Mode or administrator privileges (`0x80073CFF`).
- **Zero Fake Passes**: Core functionality (imaging, 10-frame ICO, durable mutation, desktop.ini preservation, Undo, Reset, single-instance routing, crash recovery, and real WinUI 3 rendering) is **VERIFIED**. However, because the OS-level Modern Context Menu registration and WACK cannot be proven on this host without Developer Mode or elevated privileges, Gate 1 must remain **`NOT PROVEN`** until deployed to an environment with Developer Mode enabled or official Store identity.
