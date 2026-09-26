# Ornare V1 — ARM64 Platform Qualification Handoff Package

This directory provides the verified ARM64 production package, multi-architecture bundle, SHA-256 checksums, and the 19-point verification protocol required to qualify Ornare V1 on native Windows 11 ARM64 hardware (e.g., Qualcomm Snapdragon X Elite / Copilot+ PC).

---

## 1. Verified Artifacts & Checksums

- **Version:** `1.0.0.0`
- **Package Identity:** `Name="Ornare"`, `Publisher="CN=Ornare"`, `ProcessorArchitecture="arm64"`

| File | SHA-256 Checksum | Purpose |
|---|---|---|
| `Ornare_1.0.0.0_arm64.msix` | `ECD55ACAB2CADBCF1B1800B0024769BBC3BC1343C2C22AEDC521637ECBDFB05A` | Standalone native ARM64 MSIX package |
| `Ornare_1.0.0.0_x64_arm64.msixbundle` | `A2C8DBF8A6F3767CCBAED28F8C410BC1192CF238BD7AACE4F9298A3E2E96C0E5` | Multi-architecture production MSIX bundle (x64 + ARM64) |

Both packages are digitally signed with Authenticode SHA-256 and verified via binary PE audit (all 10 native binaries are `AA64 machine (ARM64)`).

---

## 2. Prerequisites on Target ARM64 Device

1. **Hardware:** Genuine ARM64 PC (e.g., Qualcomm Snapdragon X Elite / Plus, Surface Pro 11, Lenovo Yoga Slim 7x).
2. **Operating System:** Windows 11 Version 24H2 / 25H2 (Build 26100.0+ / 26200.0+, ARM64).
3. **Privileges:** Standard User account. Administrator elevation is **prohibited** for normal usage.
4. **Certificate Trust (Test Builds):**
   If installing using the development signing certificate, install the certificate to `LocalMachine\Root`:
   ```powershell
   Import-Certificate -FilePath "path\to\dev-cert.cer" -CertStoreLocation "Cert:\LocalMachine\Root"
   ```

---

## 3. Installation Instructions

### Option A: Install Standalone ARM64 Package
```powershell
Add-AppxPackage -Path ".\Ornare_1.0.0.0_arm64.msix"
```

### Option B: Install Multi-Arch Bundle (Recommended)
```powershell
Add-AppxPackage -Path ".\Ornare_1.0.0.0_x64_arm64.msixbundle"
```
Windows App Installer will automatically select and deploy the native ARM64 payload.

---

## 4. 19-Point ARM64 Runtime Qualification Protocol

Execute the following steps on the ARM64 device to produce conclusive runtime certification evidence:

| Step | Test Description | Expected Behavior |
|---|---|---|
| 1 | **Explorer Shell Extension Registration** | Right-click any standard folder in File Explorer -> Modern context menu renders "Set Folder Icon" with native Ornare icon. |
| 2 | **Process Architecture Verification** | Open Task Manager / Process Explorer -> Verify `Ornare.ShellExtension.dll` loads in-process within Explorer as `ARM64`. |
| 3 | **Context Menu Invocation** | Click "Set Folder Icon" -> Quick Panel window appears within <= 250ms with Mica backdrop and fluent animations. |
| 4 | **Native AOT Process Execution** | Check Task Manager -> `Ornare.App.exe` executes as `ARM64 (Native)`, memory footprint <= 65 MB. |
| 5 | **Drag-and-Drop Image Apply** | Drop a `.png`, `.jpg`, `.webp`, or `.svg` image onto Quick Panel -> Drop target animates to active state. |
| 6 | **ICO Generation (ARM64 Native)** | `Magick.Native-Q16-arm64.dll` processes the image and generates multi-resolution `folder.ico` (16, 24, 32, 48, 64, 128, 256px). |
| 7 | **Durable Relative Filesystem Placement** | Target folder receives hidden, system `.ornare\folder.ico` and customized `desktop.ini`. Target folder receives `+R` (ReadOnly) attribute. |
| 8 | **Passive Explorer Icon Rendering** | Explorer renders custom icon immediately via `SHChangeNotify` without restarting `explorer.exe`. |
| 9 | **Process Independence** | Close Ornare Quick Panel -> Verify icon persists in Explorer with 0 Ornare processes running. |
| 10 | **Manager Window Launch** | Launch Ornare Manager -> Window opens smoothly with Sidebar navigation and Turkish/English localization. |
| 11 | **SQLite Database Verification** | Verify `%LocalAppData%\Ornare\ornare.db` stores folder assignment, library asset, and journal history using system `winsqlite3.dll`. |
| 12 | **Folder Health Monitoring** | Manager lists the customized folder as `Healthy` with correct preview and last modified timestamp. |
| 13 | **Undo Operation** | Click Undo in Manager -> Prior icon or Windows default state is cleanly restored; owned `.ornare` folder cleaned up. |
| 14 | **Reset Operation** | Click Reset on a customized folder -> Only owned `IconResource` semantics removed; foreign `desktop.ini` sections preserved. |
| 15 | **Turkish Localization** | Change Windows display language to Turkish (`tr-TR`) -> Ornare UI displays native Turkish strings without layout truncation. |
| 16 | **System Theme Switching** | Toggle Windows between Light and Dark mode -> Mica backdrop and typography transition smoothly in real time. |
| 17 | **High DPI Scaling (150%, 200%)** | Test on 150% and 200% scaling -> Text, glyphs, and preview bitmaps remain crisp without blurring. |
| 18 | **Reboot Persistence** | Restart Windows 11 ARM64 device -> Customized folder renders custom icon passively upon boot before any app launch. |
| 19 | **External Conflict Detection** | Manually modify `desktop.ini` to a system icon -> Ornare Manager flags folder as `ExternalIconConflict` without data loss. |

---

## 5. Rollback & Clean Uninstall Instructions

To remove Ornare without affecting customized folders:
```powershell
Get-AppxPackage *Ornare* | Remove-AppxPackage
```
- Shell extension unregisters cleanly from COM surrogate and modern context menu.
- Prior customized folders continue to display their durable icons passively via standard Windows Explorer `desktop.ini` semantics.
- User data and `.ornare\folder.ico` remain intact.
