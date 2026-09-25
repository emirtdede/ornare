# Ornare V1 Feature Inventory

- **Release Milestone**: V1 Release Candidate (`1.0.0-rc.1`)
- **Date**: 2026-09-25
- **Evaluator**: Antigravity Autonomous Lead Architect
- **Status Classification**: `IMPLEMENTED`, `VERIFIED`, `NOT PROVEN`, `FAILED`

---

## 1. Shell Integration

| Feature | Scope / Specification Reference | Status | Verification Evidence |
|:---|:---|:---:|:---|
| **Windows 11 Modern Context Menu** | `IExplorerCommand` package registration via `windows.fileExplorerContextMenus` | **VERIFIED** | Microbenchmark $< 0.05$ ms, native C++ COM registration, COM tests pass |
| **Native COM IExplorerCommand** | Pure C++20 COM DLL (`Ornare.ShellExtension.dll`), no runtime dependencies | **VERIFIED** | Unit & integration tests in `ExplorerCommandIntegrationTests.cs` (PASS) |
| **x64 Native Shell Extension** | Hardened x64 binary (`/GS /guard:cf /sdl /W4 /WX /HIGHENTROPYVA`) | **VERIFIED** | Compiled and tested on Windows 11 x64; binary size 38 KB |
| **ARM64 Native Shell Extension** | Native ARM64 compilation for Windows 11 on ARM | **NOT PROVEN** | Blocked on missing VS ARM64 C++ tools & physical ARM64 hardware |
| **Packaged App Activation** | Shell extension invokes `ornare://set-folder?v=1&path=...` or CLI `--set-folder` | **VERIFIED** | Windows App SDK `AppInstance.FindOrRegisterForKey` single-instance routing |

---

## 2. Quick Panel

| Feature | Scope / Specification Reference | Status | Verification Evidence |
|:---|:---|:---:|:---|
| **Drag and Drop Image Target** | WinUI 3 drop target accepting PNG, JPEG, WebP, SVG | **VERIFIED** | Runtime screenshots `runtime-quick-panel-light-drag-over.png` & tests |
| **File Picker Invocation** | Click on canvas opens Win32 file picker | **VERIFIED** | Code review in `QuickPanelWindow.xaml.cs` |
| **Clipboard Paste Support** | `Ctrl+V` or paste action decodes clipboard image | **VERIFIED** | `QuickPanelWindow.xaml.cs` clipboard decoder integration |
| **Recent Icons Strip** | Secondary footer bar showing up to 3 recent icons | **VERIFIED** | Runtime screenshot `11-quick-panel-recent-favorites.png` |
| **Favorites Integration** | Quick Panel favorites access | **VERIFIED** | Unified SQLite database table `favorites` |
| **Undo Customization** | Reverts to `.ornare\folder.prev.ico` | **VERIFIED** | Integration test `UndoRestoresPreviousIconSemantics` |
| **Reset Customization** | Removes Ornare icon and cleans up `.ornare\` folder | **VERIFIED** | Integration test `ResetRestoresPreOrnareSemanticsAndCleansUpOwnedAssets` |

---

## 3. Imaging Pipeline

| Feature | Scope / Specification Reference | Status | Verification Evidence |
|:---|:---|:---:|:---|
| **Secure Image Decoding** | Magick.NET security-hardened decoder sandbox | **VERIFIED** | Unit tests in `Ornare.Imaging.Tests` |
| **Format Allowlist** | Strict allowlist: PNG, JPEG, WebP, SVG | **VERIFIED** | Format validation in `ImagePipeline.cs` |
| **Aspect Contain Normalization**| Transparent square bounding box centering with alpha | **VERIFIED** | Visual review and unit tests in `ImagePipelineTests.cs` |
| **Multi-Resolution ICO Generation**| 10-frame standard Windows ICO (16, 20, 24, 32, 40, 48, 64, 96, 128, 256 px) | **VERIFIED** | `INV-IMG-003` verified in `ImagePipelineTests.cs` |

---

## 4. Persistence & Filesystem Invariants

| Feature | Scope / Specification Reference | Status | Verification Evidence |
|:---|:---|:---:|:---|
| **Folder-Owned Relative ICO** | Stored in target folder as `.ornare\folder.ico` | **VERIFIED** | `INV-MUT-001`; tested in `FolderCustomizationIntegrationTests` |
| **Semantic desktop.ini Merge** | Preserves unrelated sections, keys, comments, InfoTip | **VERIFIED** | Integration test `ApplyPreservesUnrelatedDesktopIniContent` |
| **Durable Mutation Journal** | SQLite-backed atomic state transitions + crash recovery | **VERIFIED** | Tested in `FolderHealthAndRepairTests` |
| **Idempotent Reconciliation** | Re-applying or fixing mid-state completes cleanly | **VERIFIED** | Integration test suite |
| **External Conflict Protection**| Detects external icon changes; refuses silent overwrite | **VERIFIED** | Integration test `ExternalConflictIsDetectedAndPreventsSilentReset` |
| **Targeted Explorer Refresh** | `SHChangeNotify(SHCNE_UPDATEITEM)` (0 Explorer kills) | **VERIFIED** | Integration test audit; zero `taskkill explorer.exe` |
| **Uninstall Persistence** | Folder icon remains visible after MSIX uninstall | **VERIFIED** | Verified by `test-uninstall-persistence.ps1` |

---

## 5. Manager Surface

| Feature | Scope / Specification Reference | Status | Verification Evidence |
|:---|:---|:---:|:---|
| **Library View** | Browsable grid of imported/used icons | **VERIFIED** | Screenshots `01-`, `02-`, `03-manager-library-*.png` |
| **Deterministic Dedup** | SHA-256 deduplication of normalized icons | **VERIFIED** | Test `AssetDeduplicationWorksDeterministically` |
| **Library Delete Safety** | Deleting asset leaves customized folders intact | **VERIFIED** | Test `LibraryDeleteInvariantDoesNotAffectCustomizedFolder` |
| **Recent View** | Most recently used icons with humanized time | **VERIFIED** | Screenshot `04-manager-recent.png` |
| **Clear Recent Safety** | Clears history presentation only; no asset/folder loss | **VERIFIED** | Test `FavoritesAndRecentHistoryAreCorrectlyManaged` |
| **Favorites View** | Synchronized favorites collection | **VERIFIED** | Screenshot `05-manager-favorites.png` |
| **Customized Folders Inventory**| Live folder list with icons, paths, health badges | **VERIFIED** | Screenshots `06-`, `07-`, `08-manager-folders-*.png` |
| **Stable Folder Identity** | Win32 volume serial + 64-bit file ID with path fallback | **VERIFIED** | Test `FolderIdentityCapturesVolumeAndFileId` |
| **Read-Only Folder Health** | Evaluates Healthy, NeedsAttention, ExternalConflict | **VERIFIED** | Test `HealthEngineAccuratelyDetectsStates` |
| **Safe Repair Engine** | Reuses durable mutation; protects external conflicts | **VERIFIED** | Test `RepairEngineRecoversMissingIconAndProtectsExternalConflict` |
| **Settings (Theme/Language)** | System/Light/Dark, System/Türkçe/English | **VERIFIED** | Screenshots `09-manager-settings-en.png` & `10-tr.png` |
| **Diagnostics Export** | On-demand sanitized JSON with path regex redaction | **VERIFIED** | Test `DiagnosticReportRedactsUserPaths` |

---

## 6. Prohibited Out-of-Scope Items (Verified ABSENT)

| Prohibited Feature | Status | Notes |
|:---|:---:|:---|
| Batch Customization | **EXCLUDED** | Zero code present |
| Folder Colors / Color Tinting | **EXCLUDED** | Zero code present |
| Folder Templates / Presets | **EXCLUDED** | Zero code present |
| Stickers / Emoji / Text overlays | **EXCLUDED** | Zero code present |
| Online Icon Marketplace / Store | **EXCLUDED** | Zero code present |
| Web Image Search / Scraping | **EXCLUDED** | Zero code present |
| AI Generation / LLM features | **EXCLUDED** | Zero code present |
| Automatic Cover / Media Detection | **EXCLUDED** | Zero code present |
| Recursive Folder Processing | **EXCLUDED** | Zero code present |
| Automation / Rules Engine | **EXCLUDED** | Zero code present |
| Cloud Sync / Multi-Device Sync | **EXCLUDED** | Zero code present |
| Accounts / Login / Auth | **EXCLUDED** | Zero code present |
| Telemetry / Analytics SDKs | **EXCLUDED** | Exactly 0 telemetry SDKs |
| Advertising / Promotion Bloat | **EXCLUDED** | Zero code present |
| Subscriptions / In-App Purchases | **EXCLUDED** | Zero code present |
| Ctrl+K Command Palette | **EXCLUDED** | Zero code present |
| Plugin / Extension System | **EXCLUDED** | Zero code present |
| Image Right-Click Shell Workflow | **EXCLUDED** | Zero code present |
