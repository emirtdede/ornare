# Ornare V1 — ARM64 Platform Qualification Handoff Package

This directory provides the complete, self-contained handoff specification, build instructions, installation prerequisites, and 19-point verification protocol required to qualify Ornare V1 on native Windows 11 ARM64 hardware (e.g., Qualcomm Snapdragon X Elite / Copilot+ PC).

---

## 1. Context & Architecture Status

On the primary CI/development host:
- **Architecture:** AMD64 (x64) physical hardware.
- **Visual Studio Toolchain Status:** `Microsoft.VisualStudio.Component.VC.Tools.ARM64` is missing and requires administrative UAC elevation to install.
- **ARM64 Native Shell Extension:** Cannot be built without the native MSVC ARM64 toolset.
- **Rule Enforcement:** Under the Ornare engineering contract, emulation on an x64 host cannot be claimed as runtime verification. Full qualification requires execution on genuine ARM64 Windows 11 hardware.

---

## 2. Prerequisites for ARM64 Build & Validation Device

### 2.1 Build Machine Prerequisites
If building the ARM64 package from source:
1. **Windows 11 24H2 / 25H2** (x64 or ARM64 host).
2. **Visual Studio 2022 / Build Tools** with the following components installed:
   - `Microsoft.VisualStudio.Workload.NativeDesktop`
   - `Microsoft.VisualStudio.Component.VC.Tools.ARM64` (Mandatory for `Ornare.ShellExtension.vcxproj`)
   - `Microsoft.VisualStudio.Component.Windows11SDK.26100`
3. **.NET 10 SDK** (with Windows Desktop workload).
4. **PowerShell 7+** (with script execution enabled).

### 2.2 Test Device Prerequisites (Physical Windows 11 ARM64 PC)
1. **Operating System:** Windows 11 Version 24H2 / 25H2 (Build 26100.0+ / 26200.0+, ARM64).
2. **Account:** Standard User account (UAC elevation must NOT be required during normal app operation).
3. **Developer Mode or Certificate Trust:** If installing a test-signed package, the test signing certificate must be trusted in `Cert:\LocalMachine\Root`.

---

## 3. ARM64 Compilation & Packaging Commands

Once the `Microsoft.VisualStudio.Component.VC.Tools.ARM64` component is installed:

### Step 1: Build Native Shell Extension (ARM64 Release)
```powershell
msbuild src/Ornare.ShellExtension/Ornare.ShellExtension.vcxproj `
    /p:Configuration=Release `
    /p:Platform=ARM64 `
    /p:TreatWarningAsError=true `
    /p:SpectreMitigation=Spectre `
    /p:ControlFlowGuard=Guard
```
*Verify output binary:*
```powershell
dumpbin /headers src/Ornare.ShellExtension/bin/ARM64/Release/Ornare.ShellExtension.dll | Select-String "machine \(ARM64\)"
```

### Step 2: Build & Publish Managed Projects (ARM64 Release)
```powershell
dotnet publish src/Ornare.App/Ornare.App.csproj `
    -c Release `
    -r win-arm64 `
    --self-contained false `
    /p:Platform=ARM64
```

### Step 3: Package ARM64 MSIX
```powershell
MakeAppx.exe pack /d <staging_directory> /p production_artifacts/release/candidate/Ornare_1.0.0.0_arm64.msix /nv
SignTool.exe sign /fd SHA256 /f <cert.pfx> /p <password> production_artifacts/release/candidate/Ornare_1.0.0.0_arm64.msix
```

### Step 4: Create Multi-Architecture Bundle (.msixbundle)
```powershell
MakeAppx.exe bundle /d production_artifacts/release/bundle_staging /p production_artifacts/release/candidate/Ornare_1.0.0.0_MultiArch.msixbundle
```

---

## 4. The 19-Point ARM64 Runtime Verification Protocol

The following 19 test scenarios must be executed sequentially on the physical ARM64 Windows 11 device:

| Step # | Test Scenario | Operational Procedure | Expected Result | Result (PASS/FAIL) |
|---|---|---|---|---|
| **01** | **Package Install** | Execute `Add-AppxPackage -Path Ornare_1.0.0.0_arm64.msix` as standard user. | Package installs cleanly with `Status: Ok`; zero UAC elevation prompt. | `[ ]` |
| **02** | **Explorer Modern Context Menu** | Right-click any normal local folder on an NTFS drive. | "Set Folder Icon" appears in the primary Windows 11 modern context menu with Ornare glyph. | `[ ]` |
| **03** | **Set Folder Icon Invocation** | Click "Set Folder Icon" on the selected folder. | Quick Panel launches without flicker; `GetState` latency < 10 ms. | `[ ]` |
| **04** | **Quick Panel Display & Motion** | Observe Quick Panel rendering in Light and Dark mode. | WinUI 3 Mica background, fluid animation, no card-nesting anti-patterns. | `[ ]` |
| **05** | **PNG Apply** | Drag-and-drop a high-resolution PNG image onto Quick Panel. | Progress indicator displays; 10-frame `.ico` generated; Explorer updates icon immediately. | `[ ]` |
| **06** | **Source File Deletion** | Delete the original source PNG image from the disk. | Folder icon persists unchanged; `.ornare\folder.ico` remains valid. | `[ ]` |
| **07** | **Folder Rename** | Rename the customized folder. | Icon persists without reverting; relative `desktop.ini` path intact. | `[ ]` |
| **08** | **Same-Volume Move** | Move the customized folder to another directory on the same drive. | Custom icon renders immediately without path breakage. | `[ ]` |
| **09** | **Undo Customization** | Open Quick Panel or Manager and click "Undo". | Previous icon semantics restored; `.ornare` folder cleaned up if no prior assets. | `[ ]` |
| **10** | **Reset Customization** | Apply a new icon, then click "Reset to Default". | Folder reverts to default Windows folder glyph; foreign `desktop.ini` lines preserved. | `[ ]` |
| **11** | **Manager Surface Launch** | Launch Ornare Manager from Start Menu. | Manager opens with full navigation (Library, Recent, Favorites, Folders, Settings). | `[ ]` |
| **12** | **Library Management** | Import assets into Library; delete an asset from Library. | Library updates; customized folder using that asset retains its icon (isolated copy invariant). | `[ ]` |
| **13** | **Favorites Toggle** | Star/favorite an asset in Library. | Asset appears in Favorites tab; persisted across app restarts. | `[ ]` |
| **14** | **Folder Health Scan** | Open "Customized Folders" view in Manager. | Scans active folders; accurately displays `Healthy`, `MissingIcon`, or `Conflict`. | `[ ]` |
| **15** | **Repair Engine** | Simulate a damaged attribute (`attrib -R <folder>`); run "Repair". | Repair restores `+R` attribute and refreshes Explorer shell immediately. | `[ ]` |
| **16** | **Uninstall Persistence** | Remove package via `Remove-AppxPackage`; inspect folder. | **Invariant Verified:** Custom icon, `.ornare\folder.ico`, and `desktop.ini` survive package removal. | `[ ]` |
| **17** | **Clean Reinstall** | Re-run `Add-AppxPackage`; open Manager. | Manager reattaches to existing customizations without database corruption. | `[ ]` |
| **18** | **WACK on ARM64** | Run `appcert.exe test -apptype desktop -packagefullname <PackageFullName>`. | All 22 tests pass; zero blocking failures. | `[ ]` |
| **19** | **Memory & CPU Profiling** | Monitor Task Manager during 10-minute idle period. | Idle CPU: 0.0%; Quick Panel Working Set < 50 MB; Manager Working Set < 80 MB. | `[ ]` |

---

## 5. Test Completion & Evidence Collection

Upon completing the protocol on an ARM64 Windows 11 device:
1. Export the command line output log to `production_artifacts/release/arm64-validation/arm64-test-log.txt`.
2. Capture screenshot evidence of the modern context menu, Quick Panel, and Manager running natively on ARM64.
3. Record WACK report XML to `production_artifacts/release/arm64-validation/arm64-wack-report.xml`.
4. Update `gate-4-report.md` with:
   - `ARM64 TECHNICAL: READY`
   - `ARM64 RUNTIME: VERIFIED`
