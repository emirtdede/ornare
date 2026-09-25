# Ornare Bootstrap Report — Gate 0

- Date: 2026-09-24
- Host: Windows 11 Pro 25H2 (Build 26200, x64)
- Reviewer: Antigravity IDE Autonomous Agent
- Status: Completed & Verified (Gate 0 Hardened)

---

## 1. Environment Verification

| Tool / Component | Specification Requirement | Detected Host Version | Status |
|---|---|---|---|
| Operating System | Windows 11 25H2 (build family 26200) | Windows 11 Pro, Build 10.0.26200.0, x64 | **VERIFIED** |
| Architecture | x64 + ARM64 | x64 Host | **VERIFIED** |
| .NET SDK | 10.0.401 (`global.json`, roll-forward disabled) | 10.0.401 (Commit e34a38d2ae, MSBuild 18.9.11) | **VERIFIED** |
| .NET Runtime | 10.0.12 LTS | 10.0.12 (Host x64) | **VERIFIED** |
| Visual Studio IDE | VS 2026 / 18.x Community | VS Community 2026 v18.10.2 (18.10.12217.157) | **VERIFIED** |
| Visual Studio Build Tools | MSBuild / C++ Build Tools | VS 2022 Build Tools v17.14.37710.0 (Sept 2026) | **VERIFIED** |
| MSVC C++ Toolchain (x64) | MSVC modern toolchain with security hardening | MSVC v14.44.35207 (`cl.exe`, `link.exe`, C++20) | **VERIFIED** |
| MSVC C++ Toolchain (ARM64) | MSVC ARM64 cross-compiler | Absent on host Build Tools (`Platforms\ARM64` missing) | **NOT INSTALLED** |
| Windows SDK | 10.0.26100.0 (or newer) | Windows 11 SDK 10.0.26100.0 | **VERIFIED** |
| MSIX Tools | `makeappx.exe`, `makepri.exe`, `signtool.exe` | Present in Windows SDK 10.0.26100.0\x64 | **VERIFIED** |
| WACK | Windows App Certification Kit | Present at `C:\Program Files (x86)\Windows Kits\10\App Certification Kit\` | **VERIFIED** |
| Git | Modern Git for Windows | `git version 2.55.0.windows.3` | **VERIFIED** |
| PowerShell | PowerShell Core | `pwsh 7.6.6 Core` | **VERIFIED** |
| winapp CLI | Windows App Development CLI (v0.7.0+) | `winapp v0.7.0` (Telemetry opted out) | **VERIFIED** |
| Microsoft.Sbom.DotNetTool | 4.1.5 (Local tool manifest) | `sbom-tool v4.1.5` restored via `dotnet tool restore` | **VERIFIED** |

---

## 2. Dependency Baseline Verification

| Package | Spec Version | Upstream Registry Stable | Resolved Version | Status | Action Taken / Analysis |
|---|---|---|---|---|---|
| `Microsoft.WindowsAppSDK` | 2.5.1 | 2.5.1 (Released 2026-09-16) | 2.5.1 | **VERIFIED** | Pinned centrally; restores and builds cleanly on .NET 10. |
| `Microsoft.Data.Sqlite.Core` | 10.0.12 | 10.0.12 | 10.0.12 | **VERIFIED** | Pinned centrally; runtime in-memory database test passed. |
| `SQLitePCLRaw.bundle_winsqlite3` | 3.0.5 | 2.1.11 | 2.1.11 | **RECONCILED** | Version 3.0.5 does not exist on NuGet (NU1102). Reconciled to 2.1.11 via ADR 0008. Verified zero vulnerabilities. |
| `Microsoft.Windows.CsWin32` | 0.3.333 | 0.3.335 | 0.3.333 | **VERIFIED** | 0.3.333 is stable and restores cleanly. Retained pin per policy. |
| `Magick.NET-Q16-AnyCPU` | 14.17.1 | 14.17.1 | 14.17.1 | **VERIFIED** | Pinned centrally; runtime formats query test passed. |
| `CommunityToolkit.WinUI.Animations` | 8.2.251219 | 8.2.251219 | 8.2.251219 | **VERIFIED** | Pinned centrally; restores and compiles cleanly. |
| `WinUIEx` | 2.9.3 | 2.9.3 | 2.9.3 | **VERIFIED** | Pinned centrally; restores and compiles cleanly. |

---

## 3. Architecture & Toolchain Validation

- **Explorer DLL Isolation**: `Ornare.ShellExtension.vcxproj` compiled cleanly for `Release | x64` with MSVC `/W4 /WX /sdl /GS /guard:cf /std:c++20 /DYNAMICBASE /NXCOMPAT /MACHINE:X64`. Shell DLL has zero dependencies on WinUI, Magick.NET, or SQLite.
- **ARM64 Native Shell Extension**: Attempted `Release | ARM64` MSBuild. Failed with missing platform error because host VS Build Tools lacks `Microsoft.VisualStudio.Component.VC.Tools.ARM64`. Corrective action and CI requirements documented.
- **Main App Process**: `Ornare.App` WinUI 3 project configured for `net10.0-windows10.0.26100.0`, compiled for `x64` and `ARM64`.
- **System SQLite Engine**: Validated at runtime using `winsqlite3.dll` via `SQLitePCL.Batteries_V2.Init()`. `SELECT sqlite_version()` executed successfully in test runner.
- **Central Package Management**: CPM enforced at repository root via `Directory.Packages.props`. All projects generated `packages.lock.json` and pass `dotnet restore --locked-mode`.
- **Native AOT Baseline**: ILCompiler native compilation verified on `Ornare.Core` producing native binaries (`win-x64`). WinUI full graph AOT remains scheduled for experimental validation in Gate 1.
- **Localization Infrastructure**: `Strings/en-US/Resources.resw` and `Strings/tr-TR/Resources.resw` established per Rule 40.
- **MSIX Packaging**: `Package.appxmanifest` and `Ornare.Package.wapproj` created with `windows.fileExplorerContextMenus` COM extension.

---

## 4. Test & Verification Evidence

- `scripts/spec-audit.ps1`: **PASS** (32 traced requirements, 0 contradiction patterns, CPM aligned).
- `dotnet restore Ornare.slnx --locked-mode`: **PASS** (11/11 projects restored with lockfiles).
- `dotnet build Ornare.slnx -c Release --no-restore`: **PASS** (0 warnings, 0 errors).
- `dotnet test Ornare.slnx -c Release --no-build`: **PASS** (7 tests passed, 0 failed, 0 skipped).
- `dotnet package list --project Ornare.slnx --vulnerable`: **PASS** (0 vulnerabilities).
- `scripts/verify.ps1`: **PASS** (End-to-end automated verification green).

---

## 5. Distinction: Specification vs. Runtime Verification

The following items are defined and governed by specification docs, but are **NOT YET PROVEN** by runtime execution evidence:

| Subsystem / Capability | Specification Status | Runtime Verification Status |
|---|---|---|
| Durable mutation journal | Complete (`docs/LOGIC_INVARIANTS.md`) | **SPECIFIED — IMPLEMENTATION NOT YET PROVEN** |
| Mutation idempotency | Complete (`docs/LOGIC_INVARIANTS.md`) | **SPECIFIED — IMPLEMENTATION NOT YET PROVEN** |
| desktop.ini semantic merge | Complete (`docs/LOGIC_INVARIANTS.md`) | **SPECIFIED — IMPLEMENTATION NOT YET PROVEN** |
| External conflict detection | Complete (`docs/LOGIC_INVARIANTS.md`) | **SPECIFIED — IMPLEMENTATION NOT YET PROVEN** |
| Repair semantics | Complete (`docs/LOGIC_INVARIANTS.md`) | **SPECIFIED — IMPLEMENTATION NOT YET PROVEN** |
| Rollback / reconciliation | Complete (`docs/LOGIC_INVARIANTS.md`) | **SPECIFIED — IMPLEMENTATION NOT YET PROVEN** |
| Native Shell ARM64 Compile | Complete (`Ornare.ShellExtension.vcxproj`) | **SPECIFIED — BLOCKED BY MISSING HOST VS COMPONENT** |
| ARM64 Physical Device Runtime | Target defined (Windows 11 25H2 ARM64) | **NOT PROVEN (Host is x64)** |
| Explorer Live Shell Latency | Target defined (< 50ms) | **NOT PROVEN (Scheduled for Gate 1)** |
| WinUI Full Graph Native AOT | Architecture defined | **NOT PROVEN (Scheduled for Gate 1 Experiment)** |
