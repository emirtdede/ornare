# Ornare V1 Packaging and Distribution Report — Gate 3

- **Date:** 2026-09-25
- **Baseline Release Candidate:** `1.0.0-rc.1`
- **Output Artifacts Directory:** `production_artifacts/release/candidate/`
- **Package Target:** Windows 11 x64 (MSIX)
- **Status:** **VERIFIED (TECHNICAL RC APPROVED)**

---

## 1. Candidate Artifacts & Cryptographic Signatures

The official release candidate artifacts are located under `production_artifacts/release/candidate/`:

| Artifact Name | Role | SHA-256 Checksum |
|---|---|---|
| `Ornare_1.0.0.0_x64.msix` | Signed Release Candidate MSIX Package | `435DB46F2BA7848655BE1ED348E737AAB7E8578DC4C4E7424E35A4A7BF94F886` |
| `Ornare.App.exe` | WinUI 3 Application Binary (from layout) | `D8B8A58F4B060951005B5C10D7D328550919E104B8AFE07C248B586BDC8716AB` |
| `Ornare.ShellExtension.dll` | Native C++ COM Explorer Extension (from layout) | `41FCA2A9C30009FA3A66164184468797846B101E1477529A47DA5880CCBE243E` |
| `sbom.spdx.json` | SPDX 2.3 Machine-Readable Software Bill of Materials | `28D1C75224A8797AD243DC1AC542C06704CE4AD95CB3A05EE042ECD9C3CB9736` |

These values are recorded in `production_artifacts/release/candidate/SHA256SUMS.txt`.

---

## 2. Package Lifecycle Verification (Sections 68–72)

Empirically verified on Windows 11 Pro 25H2 x64 via `scripts/test-msix-lifecycle.ps1`:

1. **Fresh Install (Section 68)**:
   - `Add-AppxPackage -Path .\Ornare_1.0.0.0_x64.msix` succeeds cleanly without elevation.
   - Package registers under `PackageFullName`: `Ornare_1.0.0.0_x64__x28mb7avd5mve` with `Status: Ok`.
2. **In-Place Upgrade (Section 69)**:
   - Verified upgrading from `1.0.0.0` to `1.0.1.0` in-place using real AppX upgrade deployment.
   - User database, settings, and folder customizations (`.ornare\folder.ico` + `desktop.ini`) preserved 100% intact.
3. **Uninstall & User Data Ownership (Section 71 & 86)**:
   - `Remove-AppxPackage` cleanly removes application files and COM context menu registration.
   - **Crucial Invariant**: User folder icons, folder attributes, and `.ornare\folder.ico` remain untouched on disk. Windows Explorer continues to render folder icons.
4. **Reinstall (Section 72)**:
   - Reinstalling `Ornare_1.0.0.0_x64.msix` restores application and automatically re-associates with existing customized folders.

---

## 3. Package Content Hygiene Audit (Section 73)

The candidate package was extracted into an isolated audit directory and evaluated against forbidden content policies:

| Scanned Category | Prohibited Items | Audit Result | Status |
|---|---|---|---|
| **Debug Symbols** | `.pdb` files | **0 found** (stripped during packaging) | **PASS** |
| **Test Assemblies** | `*test*.dll`, `xunit*` | **0 found** | **PASS** |
| **Source Files** | `.cs`, `.cpp`, `.h`, project files | **0 found** | **PASS** |
| **Private Keys & Secrets** | `.pfx`, `.key`, credentials | **0 found** | **PASS** |
| **Sample Assets** | Benchmark logs, screenshots | **0 found** | **PASS** |
| **Development Scripts** | `.ps1`, `.bat`, `.cmd` | **0 found** | **PASS** |

---

## 4. Windows App Certification Kit (WACK) Evaluation (Section 102)

- Evaluated on Windows 11 SDK Certification Kit (`appcert.exe`).
- **Result**: **PASS with Advisory Warnings (22 Passed, 1 Optional, 1 Advisory Warning)**
  - Zero fatal blocking errors.
  - Advisory warning regarding DPI awareness binary manifest parse (WinUI 3 XAML handles DPI dynamically via PerMonitorV2).
  - Optional finding on third-party static string references inside DirectML/Magick native binaries.
  - Mandatory platform, security, UAC standard user, and COM registration tests passed 100%.

---

## 5. Commercial Release Readiness Separation (Section 110)

| Gate Domain | Technical RC Status | Commercial Public Release Status | Notes |
|---|---|---|---|
| **Architecture & Code** | **PASS** | **PASS** | Hardened, tested, 0 warnings. |
| **Package & Integrity** | **PASS** | **BLOCKED** | Uses local development certificate (`CN=Ornare`). Public distribution requires commercial code signing certificate or Microsoft Store ingestion signature. |
| **Brand Clearance** | N/A | **BLOCKED** | Trademark clearance across Nice Class 9 and Store name reservation must be formally completed before public marketing. |
| **Public Security Channel**| N/A | **BLOCKED** | Official Vellium public vulnerability contact must be finalized in `SECURITY.md`. |

---

## 6. Packaging Conclusion

Ornare V1 Release Candidate package satisfies all technical installation, upgrade, hygiene, and platform certification requirements:
- **Technical RC Packaging Result**: **`VERIFIED (PASS)`**
- **Public Release Status**: **`BLOCKED (Commercial Prerequisites Pending)`**
