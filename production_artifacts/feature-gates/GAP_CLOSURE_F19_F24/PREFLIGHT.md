# Gap-Closure Preflight: Features F19–F24 Certification Audit

**Audit Scope:** Features F19 (Portable Folder Mode), F20 (Folder Drag & Drop into Manager), F21 (Apply Last Icon), F22 (Undo from Explorer), F23 (Reset from Explorer), and F24 (CLI + Machine-Readable Output).  
**Normative References:**
- `feature-expansion/03_GOAL_PROMPT.md`
- `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`
- `feature-expansion/02_EXECUTION_ORDER.md`
- `feature-expansion/features/F19_portable-folder-mode.md` through `F24_cli-json.md`
- `production_artifacts/feature-gates/F19/REPORT.md` through `F24/REPORT.md`
- `docs/LOGIC_INVARIANTS.md`, `docs/ARCHITECTURE.md`, `docs/QUALITY_GATES.md`

**Status:** Preflight Established  
**Date:** 2026-09-26  

---

## 1. Mandatory Gate Requirement Mapping Matrix

| Gate Requirement | Scope / Target | Assessment Status | Detailed Rationale / Action Required |
|---|---|---|---|
| **1. Managed x64 Compilation** | All 7 managed projects (`Core`, `Imaging`, `Storage`, `Windows`, `DesignSystem`, `App`, `Cli`) | **PASS WITH EXISTING EVIDENCE** | Clean build with 0 errors and 0 warnings verified across the entire solution. |
| **2. Managed ARM64 Cross-Compilation** | All 7 managed projects | **PASS WITH EXISTING EVIDENCE** | Verified via `scripts/build-managed-arm64.ps1` (0 errors, 0 warnings). |
| **3. Automated Test Regression** | `Ornare.IntegrationTests`, `Ornare.Imaging.Tests`, `Ornare.Core.Tests`, `Ornare.Storage.Tests` | **PASS WITH EXISTING EVIDENCE** | 406 tests passed with 0 failures, 0 skipped. |
| **4. x64 Native AOT Verification** | `Ornare.App.exe`, `Ornare.Cli.exe` | **NEEDS RE-RUN** | Real `PublishAot=true` publish must be executed, verifying native PE machine type (AMD64), entry points, and zero IL/AOT warnings. |
| **5. ARM64 Native AOT Verification** | `Ornare.App.exe`, `Ornare.Cli.exe` | **NEEDS RE-RUN** | Real `PublishAot=true` cross-publish must be executed for `win-arm64`, verifying native PE machine type (AA64) and zero AOT warnings. |
| **6. Native C++ Shell Extension (x64 Release)** | `Ornare.ShellExtension.vcxproj` (Release \| x64) | **NEEDS RE-RUN** | Must compile with MSVC, verify `Machine=AMD64`, COM exports (`DllGetClassObject`, `DllCanUnloadNow`), and security hardening (DEP, ASLR, High Entropy VA, CFG, /GS). |
| **7. Native C++ Shell Extension (ARM64 Release)** | `Ornare.ShellExtension.vcxproj` (Release \| ARM64) | **NEEDS RE-RUN** | Must cross-compile with MSVC for ARM64, verify `Machine=AA64`, identical COM exports, and binary parity. |
| **8. Shell Extension Code Integrity Diff** | `src/Ornare.ShellExtension/dllmain.cpp` | **NEEDS FIX** / **NEEDS REVIEW** | F21 deleted/recreated `dllmain.cpp`. Line-by-line diff against pre-F21 commit is required to certify COM refcounting, error containment, Explorer process isolation, and lack of external dependencies. |
| **9. Test-Change Integrity Audit** | `ApplyLastIntegrationTests.cs`, `ShellExtensionFlyoutTests.cs`, `ExplorerCommandStressTests.cs` | **NEEDS REVIEW** / **NEEDS FIX** | Audit all assertion/threshold changes during F19–F24; validate `.ornare\folder.ico,0` vs `.ornare\folder.ico` against Windows shell contract; restore any unjustified threshold changes. |
| **10. Real Installed-MSIX CLI Alias Verification** | `ornare.exe` via AppExecutionAlias | **NEEDS RE-RUN** | Must build fresh signed MSIX, install locally, and verify execution from an isolated cmd/pwsh process without source-tree PATH. |
| **11. Package Payload & Hygiene Audit** | Produced MSIX layout and archive | **NEEDS RE-RUN** | Verify all binaries (App, Cli, ShellExtension) are present, PDBs stripped, no source/scratch files, valid manifest XML. |
| **12. Windows App Certification Kit (WACK)** | Built x64 MSIX package | **NEEDS RE-RUN** / **EXTERNAL IF MISSING** | Run `appcert.exe` against the MSIX candidate; record results or document exact tooling status if unavailable. |
| **13. F20 UI & Accessibility Closure** | Manager Target Tray & Multi-Folder Drag/Drop | **NEEDS RE-RUN** | Execute full UI matrix: Light/Dark/System/High Contrast, 100/125/150/200% scaling, Narrator, keyboard navigation, TR/EN resources. |
| **14. Shared Performance Protocol Verification** | Critical paths + F19–F24 additions | **NEEDS RE-RUN** | Capture >=20 samples for P95: Quick Panel cold/warm, apply latency, CLI startup, CLI inspect, CLI apply, portable reconciliation. Compare against baseline. |
| **15. Real Explorer E2E Golden Path** | Physical folder customization + Explorer refresh | **PASS WITH EXISTING EVIDENCE** | Verified via `scripts/test-real-golden-path.ps1` (0 Explorer restarts, 0 taskkill, desktop.ini integrity preserved). Will be re-run in final full regression. |
| **16. Physical ARM64 Runtime Validation** | Physical ARM64 execution | **NOT PROVEN — EXTERNAL ONLY** | Physical ARM64 hardware is not available on this build machine. Retained as explicit external gate per project contract. |
| **17. Human Linguistic Review** | New strings in TR/EN | **NOT PROVEN — EXTERNAL ONLY** | Developer resource keys and translations complete; formal human native linguistic sign-off remains external. |

---

## 2. Planned Execution Sequence

1. **Phase B:** Execute real x64 Native AOT publish for `Ornare.App` and `Ornare.Cli`. Inspect binaries.
2. **Phase C:** Execute real ARM64 Native AOT cross-publish for `Ornare.App` and `Ornare.Cli`. Inspect binaries.
3. **Phase D:** Rebuild native `Ornare.ShellExtension.vcxproj` for Release|x64 and Release|ARM64. Inspect DLL headers and security mitigations.
4. **Phase E:** Perform forensic line-by-line diff of `src/Ornare.ShellExtension/dllmain.cpp` against pre-F21 commit (`HEAD~6` / commit before F21).
5. **Phase F:** Perform test-change integrity audit and produce `production_artifacts/feature-gates/GAP_CLOSURE_F19_F24/TEST_INTEGRITY_AUDIT.md`.
6. **Phase G & H:** Assemble fresh signed MSIX package, perform package payload audit in `production_artifacts/feature-gates/GAP_CLOSURE_F19_F24/package/`, install MSIX, and verify `ornare.exe` alias in independent shell.
7. **Phase I:** Run WACK on the generated MSIX candidate.
8. **Phase J:** Perform F20 UI/Accessibility matrix review.
9. **Phase K:** Execute performance benchmark runs (>=20 samples) for all required metrics.
10. **Phase L:** Run complete automated suite regression and verification scripts.
11. **Phase M:** Amend existing F19–F24 `REPORT.md` documents.
12. **Phase N:** Create `production_artifacts/feature-gates/GAP_CLOSURE_F19_F24/REPORT.md` and commit locally.
