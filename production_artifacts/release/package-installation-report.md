# Package Installation Report — Ornare V1 MSIX

- Date: 2026-09-25
- Environment: Windows 11 Pro 25H2 (Build 10.0.26200.0, x64)
- Package Artifact: `production_artifacts/release/Ornare_1.0.0.0_x64.msix`
- Signer: `CN=Ornare` (Self-signed Development Certificate, Thumbprint: `54D6998855C4FFAC09B8F043F2A6AE0A2FD7FDF4`)
- Installation Method: `Add-AppxPackage -Path ...` (Normal signed MSIX installation without Developer Mode)
- Status: **VERIFIED (SUCCESSFUL)**

---

## 1. Verified Package Properties

| Property | Measured / Registered Value | Verification Status |
|---|---|---|
| **Name** | `Ornare` | **VERIFIED** |
| **PackageFullName** | `Ornare_1.0.0.0_x64__x28mb7avd5mve` | **VERIFIED** |
| **PackageFamilyName** | `Ornare_x28mb7avd5mve` | **VERIFIED** |
| **Publisher** | `CN=Ornare` | **VERIFIED** |
| **Architecture** | `X64` | **VERIFIED** |
| **Version** | `1.0.0.0` | **VERIFIED** |
| **InstallLocation** | `C:\Program Files\WindowsApps\Ornare_1.0.0.0_x64__x28mb7avd5mve` | **VERIFIED** |
| **Package Status** | `Ok` (Signed & Trusted in `LocalMachine\TrustedPeople`) | **VERIFIED** |

---

## 2. Package Content Verification

The installed package bundle at `C:\Program Files\WindowsApps\Ornare_1.0.0.0_x64__x28mb7avd5mve` contains:
- `Ornare.App.exe` (WinUI 3 Main Executable, Native AOT / FullTrust)
- `Ornare.ShellExtension.dll` (Native C++ COM Explorer Command Handler)
- `AppxManifest.xml` (Declares `windows.comServer` and `windows.fileExplorerContextMenus`)
- `Images\StoreLogo.png`, `Images\Square150x150Logo.png`, `Images\Square44x44Logo.png`
- `Ornare.App.pri`, `Ornare.DesignSystem.pri` (Compiled Resource Indexes)
- Runtime dependencies (`Magick.Native-Q16-x64.dll`, `WebView2Loader.dll`, etc.)
