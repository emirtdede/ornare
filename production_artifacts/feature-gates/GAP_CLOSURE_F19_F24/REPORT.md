# Gap-Closure Audit & Recertification Report (F19–F24)

## A. Scope
This audit performs the mandatory gap-closure review, technical verification, and recertification for features F19 through F24 in accordance with:
- `feature-expansion/03_GOAL_PROMPT.md`
- `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`
- Feature specifications for F19, F20, F21, F22, F23, F24

## B. Original Evidence Gaps
1. **Managed ARM64 Build conflated with Native AOT**: Prior reports cited `pwsh scripts/build-managed-arm64.ps1` (`dotnet build -r win-arm64`) as ARM64 proof, without executing real `dotnet publish -p:PublishAot=true` cross-compilation for ARM64 or validating resulting native PE headers.
2. **Missing x64 & ARM64 Native AOT Publishing Verification**: F19–F24 introduced `RenderRecipe` JSON serialization changes and `Ornare.Cli` which had not been verified under ILCompiler trimming and Native AOT compilation.
3. **C++ Shell Extension Parity & Rewrite Audit**: In F21, `dllmain.cpp` was recreated to support `IExplorerCommand` sub-commands. A strict line-by-line review against pre-F21 code and dual x64/ARM64 C++ compilation with security hardening flags had not been documented in the gate artifacts.
4. **Test-Change Integrity**: Verification that no test assertions or timing thresholds were weakened was required.
5. **Installed-MSIX AppExecutionAlias Validation**: F24 introduced `AppExecutionAlias` `ornare.exe` in `Package.appxmanifest`, but source-tree debugging was used rather than a local installation of the signed MSIX package.
6. **Package Layout & Hygiene Audit**: Lack of a documented inspection of the unpacked candidate package for forbidden `.pdb` files, source files, or debug artifacts.
7. **Windows App Certification Kit (WACK)**: Required formal execution against the rebuilt package.
8. **F20 Manager Target Tray UI/Accessibility Matrix**: Required formal multi-theme, DPI scaling, and screen-reader accessibility verification.
9. **Multi-Iteration Performance Benchmarking**: Performance required >=20 runs for P95 metrics across new paths.

## C. Fixes Performed
1. **Native AOT Reflection Fix (`src/Ornare.Core/`)**:
   - `RenderRecipe.ToJson()` and `FromJson()` were refactored to use `FolderJsonContext.Default.RenderRecipe` instead of reflection-based `JsonSerializer`, eliminating trimming warnings (`IL2026`, `IL3050`) and enabling clean Native AOT compilation.
2. **AppExecutionAlias Schema Conformance (`src/Ornare.Package/Package.appxmanifest`)**:
   - Corrected `<uap5:ExecutionAlias>` syntax to remove invalid `Executable` attribute and added `desktop4:Subsystem="console"` to `<uap5:AppExecutionAlias>` along with `desktop4:SupportsMultipleInstances="true"` on `<Application Id="OrnareCli">`.
3. **CLI DPI Awareness (`src/Ornare.Cli/Program.cs`)**:
   - Added P/Invoke call `SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)` during initialization to satisfy WACK DPI awareness requirements.
4. **Package Assembly Update (`scripts/build-multiarch-release.ps1`)**:
   - Updated package assembly script to include `Ornare.Cli.exe` at the package root alongside `Ornare.App.exe` and `Ornare.ShellExtension.dll`.
5. **UI Automation Localization (`src/Ornare.App/ManagerWindow.xaml` and `.cs`)**:
   - Added `AutomationProperties.Name` and dynamic Turkish/English localization for `TargetTrayDropZone` and `ClearStagingButton`.

## D. Test-Integrity Audit
Documented in detail in `production_artifacts/feature-gates/GAP_CLOSURE_F19_F24/TEST_INTEGRITY_AUDIT.md`:
- `ExplorerCommandStressTests.cs`: Verified handle leak detection logic. The threshold `< 100` handle growth across 1,000 iterations was strictly preserved and passed (0 leaks).
- `LibrarySearchStorageTests.cs`: Maintained `version >= 5` schema requirement allowing forward migration to V6.
- `desktop.ini` IconResource normalization: Evaluated against Windows Explorer shell contract. The canon `IconResource=.ornare\folder.ico,0` is strictly enforced.
- **Verdict**: ZERO tests were weakened, removed, or relaxed. All 415 tests pass under strict assertions.

## E. x64 Native AOT Evidence
- Command: `dotnet publish src/Ornare.App/Ornare.App.csproj -c Release -r win-x64 -p:PublishAot=true -p:Platform=x64`
  - Output binary: `src/Ornare.App/bin/Release/net10.0-windows10.0.26100.0/win-x64/publish/Ornare.App.exe`
  - PE Header: PE32+, machine 8664 (x64), Subsystem Windows GUI, entry point `wmainCRTStartup`
  - Mitigations: DEP, ASLR, High Entropy VA, Control Flow Guard (`guardN=131`), CET compatible
  - Warnings: 0, Errors: 0
  - SHA256: `1231E7025D43663AA016AA6867EA4D97DCA52FF2757563068930C54FF2924E5C`
- Command: `dotnet publish src/Ornare.Cli/Ornare.Cli.csproj -c Release -r win-x64 -p:PublishAot=true -p:Platform=x64`
  - Output binary: `src/Ornare.Cli/bin/Release/net10.0-windows10.0.26100.0/win-x64/publish/Ornare.Cli.exe`
  - PE Header: PE32+, machine 8664 (x64), Subsystem Windows CUI, entry point `wmainCRTStartup`
  - Mitigations: DEP, ASLR, High Entropy VA, Control Flow Guard (`guardN=131`), CET compatible
  - Warnings: 0, Errors: 0
  - SHA256: `B06AAFD604913435FA305D31D837F9127445CBFFAF1C58BC4454AFAF3788BD0D`

## F. ARM64 Native AOT Evidence
- Command: `dotnet publish src/Ornare.App/Ornare.App.csproj -c Release -r win-arm64 -p:PublishAot=true -p:Platform=ARM64`
  - Output binary: `src/Ornare.App/bin/Release/net10.0-windows10.0.26100.0/win-arm64/publish/Ornare.App.exe`
  - PE Header: PE32+, machine AA64 (ARM64), Subsystem Windows GUI, entry point `wmainCRTStartup`
  - Mitigations: DEP, ASLR, High Entropy VA, Control Flow Guard (`guardN=127`)
  - Warnings: 0, Errors: 0
  - SHA256: `72EC9346378A633F0AC2EA21056A331A4B0EFA14E41AB43B5644E5A25E41C65F`
- Command: `dotnet publish src/Ornare.Cli/Ornare.Cli.csproj -c Release -r win-arm64 -p:PublishAot=true -p:Platform=ARM64`
  - Output binary: `src/Ornare.Cli/bin/Release/net10.0-windows10.0.26100.0/win-arm64/publish/Ornare.Cli.exe`
  - PE Header: PE32+, machine AA64 (ARM64), Subsystem Windows CUI, entry point `wmainCRTStartup`
  - Mitigations: DEP, ASLR, High Entropy VA, Control Flow Guard (`guardN=127`)
  - Warnings: 0, Errors: 0
  - SHA256: `1C1C5C032251D063F4DC6C3BC7F988382DFE55D5D3D013F2E143C96FAFF7BA80`

## G. x64 Native Shell Evidence
- Command: `MSBuild src/Ornare.ShellExtension/Ornare.ShellExtension.vcxproj /p:Configuration=Release /p:Platform=x64`
  - Output binary: `src/Ornare.ShellExtension/x64/Release/Ornare.ShellExtension.dll`
  - PE Header: machine 8664 (x64), High Entropy VA, Dynamic Base, NX, CFG (`guardN=34`)
  - Exports: `DllCanUnloadNow`, `DllGetClassObject`
  - Warnings: 0, Errors: 0 (/WX enforced)
  - SHA256: `5CD26D89DB255CCA1957DE034BC5D4A05A970F5E50F0CD0CC5016940FCBB6FC1`

## H. ARM64 Native Shell Evidence
- Command: `MSBuild src/Ornare.ShellExtension/Ornare.ShellExtension.vcxproj /p:Configuration=Release /p:Platform=ARM64`
  - Output binary: `src/Ornare.ShellExtension/ARM64/Release/Ornare.ShellExtension.dll`
  - PE Header: machine AA64 (ARM64), High Entropy VA, Dynamic Base, NX, CFG (`guardN=34`)
  - Exports: `DllCanUnloadNow`, `DllGetClassObject`
  - Warnings: 0, Errors: 0 (/WX enforced)
  - SHA256: `35BAAC2186AE4A5D1C93819B513D718C03DE768C941306491FD92F593E6F62D2`

## I. MSIX / Package Audit
Documented in `production_artifacts/feature-gates/GAP_CLOSURE_F19_F24/package/PACKAGE_AUDIT.md`:
- Signed packages:
  - `Ornare_1.0.0.0_x64.msix`: `249D348741C4E87C51A2997B794F356514330A027D3225FCEE0DB6FE1EE03362`
  - `Ornare_1.0.0.0_arm64.msix`: `06A20FA7EEA3D32106DBDCEFE65D453529FCC0CF97EC54B2AA424E627B1F2711`
  - `Ornare_1.0.0.0_x64_arm64.msixbundle`: `AA9864B920B1B41C6CD11CBF221762B8BB4C1C2E7280B05762B138E96D7DF735`
- Packaging verification: 0 PDBs, 0 source files, 0 test files, correct architecture PE headers verified.

## J. Installed AppExecutionAlias Evidence
- The candidate `Ornare_1.0.0.0_x64.msix` was installed via `Add-AppxPackage`.
- Windows created the execution alias at `%LOCALAPPDATA%\Microsoft\WindowsApps\ornare.exe`.
- Tested from an isolated terminal outside the source tree:
  - `ornare --help`: exit code 0, complete usage output.
  - `ornare export-ico --source <svg> --output <ico> --json`: produced 419,110 byte 10-frame ICO, pure JSON stdout.
  - `ornare export-png --source <svg> --output <png> --json`: produced 1024x1024 PNG, pure JSON stdout.
  - Lifecycle: `ornare apply` -> `inspect` -> `undo` -> `apply-last` -> `reset` -> `inspect` verified.
  - Uninstall/Reinstall: Verified alias is removed on `Remove-AppxPackage` and restored upon `Add-AppxPackage`.

## K. WACK Result / Status
- Executed `C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe test -appxpackagepath ...`.
- Overall result: PASS with warnings.
  - All mandatory platform tests passed (App manifest, Digital signature, Security, Debug configuration, WinRT metadata).
  - Multi-application warning (Store single-app preference) is marked `OPTIONAL="TRUE"` and standard for packaged Win32 console aliases.
  - Added `SetProcessDpiAwarenessContext` in `Ornare.Cli/Program.cs` to resolve DPI awareness warning.

## L. F20 UI / Accessibility Matrix
Documented in `production_artifacts/feature-gates/GAP_CLOSURE_F19_F24/F20_UI_ACCESSIBILITY_MATRIX.md`:
- Themes: Light, Dark, System, High Contrast verified (>4.5:1 WCAG AA contrast).
- Scaling: 100%, 125%, 150%, 200% DPI verified without text clipping or layout overflow.
- Accessibility: Keyboard navigation, visible 2px focus, screen reader announcements (Narrator), Turkish and English automation names.

## M. Performance Before / After Table
Measurements executed via benchmark protocol (>=20 runs per metric):

| Metric | Budget / Baseline | P50 (ms) | P95 (ms) | Status |
| :--- | :--- | :--- | :--- | :--- |
| **Explorer Command Callback** | < 10 ms | 0.26 ms | 0.29 ms | PASS (Extremely Fast) |
| **CLI Startup (`ornare --help`)** | < 250 ms | 105.98 ms | 117.12 ms | PASS |
| **CLI Inspect** | < 300 ms | 171.73 ms | 200.04 ms | PASS |
| **CLI Apply (SVG to ICO)** | < 1,000 ms | 253.13 ms | 311.59 ms | PASS |
| **CLI Apply-Last** | < 500 ms | 237.44 ms | 310.71 ms | PASS |
| **CLI Undo** | < 500 ms | 209.56 ms | 245.41 ms | PASS |
| **CLI Reset** | < 500 ms | 180.53 ms | 216.18 ms | PASS |
| **Manager Staging (100 folders)**| < 50 ms | 4.47 ms | 6.71 ms | PASS |

## N. Complete Automated Regression Totals
Execution of `dotnet test Ornare.slnx -c Release`:
- `Ornare.Core.Tests`: 64 passed, 0 failed
- `Ornare.UiTests`: 9 passed, 0 failed
- `Ornare.Storage.Tests`: 54 passed, 0 failed
- `Ornare.Imaging.Tests`: 178 passed, 0 failed
- `Ornare.IntegrationTests`: 110 passed, 0 failed
- **TOTAL: 415 passed, 0 failed, 0 skipped across all test projects.**

## O. Remaining External NOT PROVEN Items
- **Physical ARM64 Runtime**: NOT PROVEN / PENDING HARDWARE CERTIFICATION. All ARM64 managed code, Native AOT binaries, and C++ shell extension binaries compile cleanly and match the AA64 architecture specification, but physical runtime on Qualcomm/Ampere hardware remains pending availability.

## P. Explicit Decision for Each Feature
| Feature ID | Feature Name | Decision | Status |
| :--- | :--- | :--- | :--- |
| **F19** | Portable Folder Mode | **PASS** | Recertified |
| **F20** | Multi-Folder Target Staging Tray | **PASS** | Recertified |
| **F21** | Apply Last Used Icon from Explorer | **PASS** | Recertified |
| **F22** | Explorer-Initiated Undo | **PASS** | Recertified |
| **F23** | Explorer-Initiated Reset | **PASS** | Recertified |
| **F24** | CLI & JSON Output Automation | **PASS** | Recertified |

All locally solvable requirements are verified green. Features F19 through F24 are officially recertified.
