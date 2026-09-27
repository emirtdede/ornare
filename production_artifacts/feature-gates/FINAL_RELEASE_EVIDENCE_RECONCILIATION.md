# Ornare — Final Release Evidence Reconciliation Report

**Document:** `production_artifacts/feature-gates/FINAL_RELEASE_EVIDENCE_RECONCILIATION.md`  
**Execution Target:** Ornare V1 Release Candidate  
**Date of Certification:** 2026-09-27  
**Certifying Engineer:** Principal Windows / C# / C++ Shell / Release Engineer  
**Final Release Candidate Verdict:** **INITIAL X64 RELEASE CANDIDATE: READY**

---

## A. Certified Git HEAD

- **Commit Hash:** `2f576e5face1041944bed10bce7c87370fb1390d`
- **Commit Subject:** `fix(imaging): update Windows 11 folder icon template to assets/Windows_11_FOLDER.svg`
- **Branch:** `main`
- **Recent Git Log (Last 10 Commits):**
  - `2f576e5` fix(imaging): update Windows 11 folder icon template to assets/Windows_11_FOLDER.svg
  - `67deec0` docs(audit): publish final expansion acceptance audit report (F01-F27 PASS)
  - `f014f08` docs(F11): record per-size optical rendering as deferred by product decision
  - `178d375` feat(F27): localization expansion to 6 locales with native shell strings and resw parity
  - `948fa97` feat(ui): implement F26 keyboard-first workflow and focus navigation
  - `24ae39d` feat(core): implement F25 dry-run operation planning and CLI --dry-run
  - `ca9306c` chore(gate): certify F19-F24 gap closure and multiarch AOT verification
  - `aff2ee2` feat(F24): CLI and machine-readable JSON output
  - `379e6a2` feat(F23): reset from explorer with flyout command and live semantic merge
  - `5005519` feat(F22): undo from explorer with flyout command and safe conflict handling

---

## B. Clean Working Tree Verification

- **Working Tree State:** All changes strictly correspond to:
  1. Release-approved public manifest localization policy (`Package.appxmanifest` restricting advertised languages to `en-US` and `tr-TR`).
  2. Updated audit & reconciliation documentation (`FINAL_EXPANSION_AUDIT.md`, `F27/REPORT.md`).
  3. Release candidate assembly and performance verification scripts (`scripts/build-rc-candidate.ps1`, `scripts/benchmark-final-rc.ps1`, `scripts/test-rc-e2e.ps1`, `scripts/test-persistence-scenarios.ps1`).
  4. Fresh WACK certification report (`wack_fresh_x64_report.xml`).
- **Hygiene Verification:**
  - Zero unexplained modified production source files.
  - Zero scratch files or temporary diagnostic code in production packages.
  - Zero untracked release-critical sources.
  - Zero generated test junk in repository root.

---

## C. Final Feature Progression Status

The approved expansion scope is complete according to the approved product decisions:
- **26 Implemented Features:** **PASS** (F01–F10, F12–F27)
- **F11 Per-Size Optical Rendering:** **DEFERRED BY PRODUCT DECISION**
- **Physical ARM64 Runtime:** **NOT PROVEN / PENDING HARDWARE CERTIFICATION**

### Feature Breakdown Matrix
| ID | Feature Name | Target Layer | Status | Release Disposition |
|:---:|---|---|:---:|---|
| **F01** | Curated Folder Colors | Quick Panel, Imaging | **PASS** | Implemented & Certified |
| **F02** | Custom Color Picker & Swatches | Quick Panel, ColorPicker | **PASS** | Implemented & Certified |
| **F03** | Versioned Folder Style Templates | Imaging, Classic/Soft/Flat | **PASS** | Implemented & Certified |
| **F04** | Status Presets & Badging | Manager, FolderCustomizer | **PASS** | Implemented & Certified |
| **F05** | Visual Priority Presets | Manager, FolderCustomizer | **PASS** | Implemented & Certified |
| **F06** | Image Placement (Contain/Cover/Fill) | Imaging, Quick Panel | **PASS** | Implemented & Certified |
| **F07** | Inset Transparent Padding | Imaging, Quick Panel | **PASS** | Implemented & Certified |
| **F08** | Solid Background Color Mode | Imaging, Quick Panel | **PASS** | Implemented & Certified |
| **F09** | Semantic SVG Recoloring | Imaging | **PASS** | Implemented & Certified |
| **F10** | Adaptive Contrast & Luminance Palette | Imaging | **PASS** | Implemented & Certified |
| **F11** | Per-Size Optical Rendering | Imaging | **DEFERRED** | Deferred by Product Decision (ADR-0011) |
| **F12** | ICO Ingestion & Multi-Res Validation | Manager, Imaging | **PASS** | Implemented & Certified |
| **F13** | Multi-Resolution ICO Export | Manager, Imaging | **PASS** | Implemented & Certified |
| **F14** | High-Resolution PNG Export | Manager, Imaging | **PASS** | Implemented & Certified |
| **F15** | Asset Tagging & Tag Filtering | Storage, Manager | **PASS** | Implemented & Certified |
| **F16** | Custom Icon Collections | Storage, Manager | **PASS** | Implemented & Certified |
| **F17** | Indexed Library Search | Storage, Manager | **PASS** | Implemented & Certified |
| **F18** | Asset Deduplication & Render Cache | Storage, Imaging | **PASS** | Implemented & Certified |
| **F19** | Portable Folder Mode & NTFS Identity | Windows, Storage | **PASS** | Implemented & Certified |
| **F20** | Folder Drag & Drop Staging Tray | Manager | **PASS** | Implemented & Certified |
| **F21** | Apply Last Icon Command | Core, Explorer, CLI | **PASS** | Implemented & Certified |
| **F22** | Undo from Explorer Secondary Command | Shell Extension, Core | **PASS** | Implemented & Certified |
| **F23** | Reset from Explorer Secondary Command | Shell Extension, Core | **PASS** | Implemented & Certified |
| **F24** | CLI & Machine-Readable JSON Output | Ornare.Cli | **PASS** | Implemented & Certified |
| **F25** | Dry-Run Operation Planning | Core, CLI | **PASS** | Implemented & Certified |
| **F26** | Keyboard-First Workflow & Accessibility | WinUI Surfaces | **PASS** | Implemented & Certified |
| **F27** | Localization Expansion (6 Locales) | App, ShellExtension, Resw | **PASS** | Implemented & Certified |

---

## D. Fresh Full Automated Regression Results

Run against CURRENT HEAD across all 5 solution test projects:

```
Command: dotnet test --verbosity normal
```

| Test Project | Total Tests | Passed | Failed | Skipped | Status |
|---|:---:|:---:|:---:|:---:|:---:|
| `Ornare.Core.Tests` | 64 | 64 | 0 | 0 | **PASS** |
| `Ornare.Storage.Tests` | 54 | 54 | 0 | 0 | **PASS** |
| `Ornare.UiTests` | 16 | 16 | 0 | 0 | **PASS** |
| `Ornare.Imaging.Tests` | 179 | 179 | 0 | 0 | **PASS** |
| `Ornare.IntegrationTests` | 120 | 120 | 0 | 0 | **PASS** |
| **TOTAL REGRESSION SUITE** | **433** | **433** | **0** | **0** | **100% PASS** |

- **Zero Failures.**
- **Zero Unexpected Skipped Tests.**

---

## E. Fresh x64 Native AOT Verification (Current HEAD)

- **Configuration:** `Release`, `win-x64`, `PublishAot=true`
- **Compiler / Toolchain:** .NET 10 ILC Native AOT Compiler (`Microsoft.DotNet.ILCompiler 10.0.12`)
- **Compilation Diagnostics:** 0 Warnings, 0 Errors.

### Binaries Certified
1. **`Ornare.App.exe`**
   - Output Path: `src/Ornare.App/bin/Release/net10.0-windows10.0.26100.0/win-x64/publish/Ornare.App.exe`
   - File Size: 13,382,656 bytes (~12.76 MB)
   - PE Machine Type: `0x8664` (`machine (x64)`) via `dumpbin /headers`
   - SHA-256: `59433DAA8748BA72AFA76E3474F46B6482795AE677D20733AD6E11B4AD477C7F`

2. **`Ornare.Cli.exe`**
   - Output Path: `src/Ornare.Cli/bin/Release/net10.0-windows10.0.26100.0/win-x64/publish/Ornare.Cli.exe`
   - File Size: 8,826,880 bytes (~8.41 MB)
   - PE Machine Type: `0x8664` (`machine (x64)`) via `dumpbin /headers`
   - SHA-256: `8B299FEDB864EB90D1442B0B6ABC634453C3D2437C35EA36F1F5080E6633FF26`

---

## F. Fresh ARM64 Native AOT Verification (Current HEAD)

- **Configuration:** `Release`, `win-arm64`, `PublishAot=true`
- **Compiler / Toolchain:** .NET 10 ILC Native AOT Compiler (`Microsoft.DotNet.ILCompiler 10.0.12`)
- **Compilation Diagnostics:** 0 Warnings, 0 Errors.

### Binaries Certified
1. **`Ornare.App.exe`**
   - Output Path: `src/Ornare.App/bin/Release/net10.0-windows10.0.26100.0/win-arm64/publish/Ornare.App.exe`
   - PE Machine Type: `0xAA64` (`machine (ARM64)`) via `dumpbin /headers`
   - SHA-256: `13E38AA0616B73BB9FADB999C66C24C2049950D42DA61776854A597DF33FC64D`

2. **`Ornare.Cli.exe`**
   - Output Path: `src/Ornare.Cli/bin/Release/net10.0-windows10.0.26100.0/win-arm64/publish/Ornare.Cli.exe`
   - PE Machine Type: `0xAA64` (`machine (ARM64)`) via `dumpbin /headers`
   - SHA-256: `BA43AC36D5F8F6B440AA2D744F8B6FAF26FB21FE5BE47CCF2AF0C90F47A791E0`

*Physical Runtime Status:* **NOT PROVEN / PENDING HARDWARE CERTIFICATION** (Preserved in multiarch pipeline; not certified for physical ARM64 runtime).

---

## G. Fresh Native Shell Extension Build: x64

- **Project:** `src/Ornare.ShellExtension/Ornare.ShellExtension.vcxproj` (Fresh Rebuild post-F27)
- **Configuration:** `Release | x64`
- **Compiler / Toolchain:** MSVC 14.44.35207 (`CL.exe HostX86\x64`, MSBuild 17.14.60)
- **Build Timestamp:** 2026-09-27 04:06:11 UTC
- **Output Path:** `src/Ornare.ShellExtension/x64/Release/Ornare.ShellExtension.dll`
- **Size:** 464,384 bytes
- **PE Machine:** `0x8664` (`machine (x64)`)
- **SHA-256:** `2C9D2FCF74403CF217FB2AE77F407E0684205A47992392BD7D4361C823A7FA66`
- **COM Exports (dumpbin /exports):**
  - Ordinal 1: `DllCanUnloadNow`
  - Ordinal 2: `DllGetClassObject`
- **Hardening Flags Preserved:**
  - Control Flow Guard (`/guard:cf`): Enabled
  - Dynamic Base (`/DYNAMICBASE`): Enabled (ASLR)
  - NX Compatible (`/NXCOMPAT`): Enabled (DEP)
  - Security Checks (`/GS`, `/sdl`): Enabled
- **Build Result:** **PASS** (0 warnings, 0 errors)

---

## H. Fresh Native Shell Extension Build: ARM64

- **Project:** `src/Ornare.ShellExtension/Ornare.ShellExtension.vcxproj` (Fresh Rebuild post-F27)
- **Configuration:** `Release | ARM64`
- **Compiler / Toolchain:** MSVC 14.44.35207 (`CL.exe HostX86\arm64`, MSBuild 17.14.60)
- **Build Timestamp:** 2026-09-27 04:06:17 UTC
- **Output Path:** `src/Ornare.ShellExtension/ARM64/Release/Ornare.ShellExtension.dll`
- **Size:** 462,336 bytes
- **PE Machine:** `0xAA64` (`machine (ARM64)`)
- **SHA-256:** `0ACBF0CF41920750FA228079D81AF9C3B059400816F9BA206CEA192FAC8F2E0F`
- **COM Exports (dumpbin /exports):**
  - Ordinal 1: `DllCanUnloadNow`
  - Ordinal 2: `DllGetClassObject`
- **Hardening Flags Preserved:**
  - Control Flow Guard (`/guard:cf`): Enabled
  - Dynamic Base (`/DYNAMICBASE`): Enabled (ASLR)
  - NX Compatible (`/NXCOMPAT`): Enabled (DEP)
  - Security Checks (`/GS`, `/sdl`): Enabled
- **Build Result:** **PASS** (0 warnings, 0 errors)

---

## I. Final Public Locale Scope Policy

In accordance with product-quality release requirements, linguistic certification is separated from technical parity:

| Locale | Language | Technical Resw Parity | Linguistic Status | Public Manifest Exposure |
|:---:|:---:|:---:|:---:|:---:|
| `en-US` | English (United States) | 198 / 198 (100%) | **OWNER_ACCEPTED** | Advertised in `Package.appxmanifest` |
| `tr-TR` | Türkçe (Turkey) | 198 / 198 (100%) | **OWNER_ACCEPTED** | Advertised in `Package.appxmanifest` |
| `de-DE` | Deutsch (Germany) | 198 / 198 (100%) | **FUNCTIONAL_DRAFT** | Retained in source; Unadvertised |
| `fr-FR` | Français (France) | 198 / 198 (100%) | **FUNCTIONAL_DRAFT** | Retained in source; Unadvertised |
| `es-ES` | Español (Spain) | 198 / 198 (100%) | **FUNCTIONAL_DRAFT** | Retained in source; Unadvertised |
| `pt-BR` | Português (Brasil) | 198 / 198 (100%) | **FUNCTIONAL_DRAFT** | Retained in source; Unadvertised |

- **Manifest Declaration:** `Package.appxmanifest` declares `<Resource Language="en-US"/>` and `<Resource Language="tr-TR"/>`.
- **Preserved Source Assets:** All 6 `.resw` catalogs remain in `src/Ornare.App/Strings/` and pass automated key/placeholder regression tests.

---

## J. Fresh x64 Release Candidate Package

- **Package Path:** `production_artifacts/release/candidate/Ornare_1.0.0.0_x64.msix`
- **SHA-256 Hash:** `5799EC6EB875EB221A116512A500FA0A15EA88FE63E0B4D41E4EFF66182FA89C`
- **Package Size:** 34.8 MB
- **Signature:** Authenticode signed with development certificate (`CN=Vellium`, SHA-1 Thumbprint: `2159112CBF6E9F647006D998CF42D23F45F14DA1`)
- **Package Contents Audit:**
  - `Ornare.App.exe` (x64 Native AOT)
  - `Ornare.Cli.exe` (x64 Native AOT)
  - `Ornare.ShellExtension.dll` (x64 Native C++)
  - `resources.pri` (Compiled root resource index)
  - `AppxManifest.xml` (With `ornare.exe` AppExecutionAlias)
  - Production tile & asset imagery
- **Hygiene Audit (Section 73):** **PASSED** (0 PDBs, 0 source files, 0 test binaries, 0 scratch files, 0 ARM64 binaries).
- **Binary Architecture Audit:** **PASSED** (All 11 contained binaries audited as `machine (x64)`).

---

## K. Installed-Package Real Runtime Test Results

Package installed via `Add-AppxPackage -Path Ornare_1.0.0.0_x64.msix` and executed without source-tree dependencies:

1. **Explorer Context Menu Activation:**
   - In-process COM invocation of `IExplorerCommand::Invoke` from simulated Explorer shell.
   - Localized verb: "Klasör Simgesini Ayarla" (matching system Turkish locale).
   - Dynamic launch: HWND `0x9E13E8`, Class `WinUIDesktopWin32WindowClass`, Title `Ornare`, Size `420x330` confirmed visible.
2. **Real Full Lifecycle on Temporary Folder:**
   - Initial Inspect: `isCustomized: false`, `desktopIniExists: false` -> **PASS**
   - Apply Dry-Run: Validated 9 planned actions, 0 blockers, emitted schemaVersion 1 JSON -> **PASS**
   - Real Apply: Generated `.ornare\folder.ico`, updated `desktop.ini`, applied System directory attribute, notified Explorer (`SHCNE_UPDATEITEM`) -> **PASS**
   - Inspect After Apply: `isCustomized: true`, `desktopIniExists: true`, `state: Healthy` -> **PASS**
   - Undo: Restored uncustomized pre-application snapshot -> **PASS**
   - Apply-Last: Successfully reapplied last icon recipe -> **PASS**
   - Reset: Completely removed customization and attributes -> **PASS**
   - Final Inspect: `isCustomized: false`, `desktopIniExists: false` -> **PASS**
3. **Persistence Scenarios:**
   - Source Deletion Persistence: Deleting original image file leaves `.ornare\folder.ico` intact and folder customized -> **PASS**
   - Rename Persistence: Renaming folder maintains valid icon via relative path and NTFS FileId64 tracking -> **PASS**
   - Same-Volume Move Persistence: Moving folder preserves customization and NTFS FileId64 assignment -> **PASS**

---

## L. Installed CLI Alias Results

Executed from clean terminal using Windows `AppExecutionAlias`:
- Resolved Path: `C:\Users\emir\AppData\Local\Microsoft\WindowsApps\ornare.exe` (Zero source-tree binary involvement).
- Commands Tested: `ornare --help`, `ornare inspect`, `ornare apply`, `ornare undo`, `ornare apply-last`, `ornare reset`.
- Output: Valid locale-neutral `schemaVersion 1` JSON across all commands.

---

## M. Windows App Certification Kit (WACK) Results

Fresh execution of Windows App Certification Kit against candidate package:

- **Target Package:** `production_artifacts/release/candidate/Ornare_1.0.0.0_x64.msix`
- **Target SHA-256:** `5799EC6EB875EB221A116512A500FA0A15EA88FE63E0B4D41E4EFF66182FA89C`
- **WACK Version:** 10.0.26100.7705 (`Centennial` validation engine)
- **Report Path:** `production_artifacts/release/candidate/wack_fresh_x64_report.xml`
- **Report Generation Time:** 27.09.2026 07:09:13
- **Overall Result:** `<REPORT xml:lang="tr-TR" OVERALL_RESULT="PASS" ...>`
- **Security & Capability Violations:** 0

---

## N. Dependency, Security, and Provenance Audit

- **Vulnerability Audit (`dotnet list package --vulnerable --include-transitive`):**
  - All 12 projects audited against `https://api.nuget.org/v3/index.json`.
  - Zero vulnerable packages found across all direct and transitive dependencies.
- **Third-Party Notices & Licenses:**
  - `Magick.NET` (14.17.1): Apache-2.0
  - `Microsoft.Data.Sqlite.Core` (10.0.12): MIT
  - `SQLitePCLRaw.bundle_winsqlite3` (2.1.11): Apache-2.0 (binds to OS `winsqlite3.dll`)
  - `Microsoft.WindowsAppSDK` (2.5.1): Microsoft Software License Terms
  - `System.CommandLine` (2.0.0): MIT
  - `WinUIEx` (2.9.3): MIT
  - `Fluent UI System Icons`: MIT
- **Provenance & Hygiene:**
  - Zero GPL, AGPL, or unauthorized assets in production binaries.
  - Zero Apple proprietary assets (SF Pro, Apple Design Resources).
  - Authentic Windows 11 folder SVG sourced from `assets/Windows_11_FOLDER.svg`.

---

## O. Final Performance Recheck Results

Executed using the 20-repeat measurement protocol on CURRENT HEAD:

| Metric | Protocol / Sample Count | Budget | Measured P50 | Measured P95 | Status |
|---|:---:|:---:|:---:|:---:|:---:|
| **Explorer COM Callback** | 20 runs | <= 15 ms | < 0.1 ms | < 0.1 ms | **PASS** |
| **CLI Startup (`ornare --help`)** | 20 runs | <= 150 ms | 89.25 ms | 106.43 ms | **PASS** |
| **CLI Inspect Latency** | 20 runs | <= 250 ms | 147.21 ms | 196.96 ms | **PASS** |
| **CLI Dry-Run Latency** | 20 runs | <= 250 ms | 148.04 ms | 165.75 ms | **PASS** |
| **Representative Apply** | 20 runs | <= 500 ms | 188.53 ms | 252.11 ms | **PASS** |
| **Cache Hit Apply** | 20 runs | <= 350 ms | 182.59 ms | 237.44 ms | **PASS** |
| **Quick Panel Cold Visible** | 3 runs | <= 800 ms | 426.15 ms | — | **PASS** |
| **Quick Panel Warm Activation** | 3 runs | <= 200 ms | 121.48 ms | — | **PASS** |
| **Quick Panel Working Set** | Monitored | <= 180 MB | 114.68 MB | — | **PASS** |
| **Idle / Background CPU** | Monitored | 0.00% | 0.00% | 0.00% | **PASS** |

*Strict Zero Background Daemon Invariant:* Zero persistent background processes exist after window closure.

---

## P. External Unproven Items

The following items are explicitly tracked as pending external conditions:
1. **Physical ARM64 Runtime:** Native AOT compilation, ARM64 PE32+ `0xAA64` machine type, and multi-architecture build packaging are 100% verified. In accordance with Section 2 of the master prompt, physical runtime is tracked as `NOT PROVEN / PENDING HARDWARE CERTIFICATION` pending physical ARM64 qualification hardware.
2. **Linguistic Native Certification for Draft Locales (`de-DE`, `fr-FR`, `es-ES`, `pt-BR`):** 100% technical resource and placeholder parity verified. Professional native human linguistic review remains pending; these locales remain unadvertised in public package manifests.

---

## Q. Open Issues Log

- **P0 (Release Blockers):** 0
- **P1 (Critical):** 0
- **P2 (Non-blocking):** 0

---

## Release Verdict

All locally verifiable requirements across automated regression (433/433), Native AOT compilation (x64 & ARM64), native shell extension hardening, installed package runtime lifecycle, Windows App Certification Kit (PASS), dependency security audit, and performance budgets are completely satisfied.

**INITIAL X64 RELEASE CANDIDATE: READY**
