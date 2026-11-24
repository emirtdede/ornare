# Ornare - Requirements Traceability

Every numbered requirement maps to its implementation and to the tests that prove it. The
`F##` identifiers are used in code comments and must stay in sync with this table. A new
requirement adds a row; a removed requirement keeps its row and is marked retired.

| ID | Requirement | Implementation | Verification |
|----|-------------|----------------|--------------|
| F00 | Colour parsing and canonical hex normalization | `src/Ornare.Core/BuiltInColors.cs` | `tests/Ornare.Imaging.Tests/BackgroundColorTests.cs` |
| F01 | Twelve curated folder colours (Fixed Decision 2) | `src/Ornare.Core/BuiltInColors.cs` | `tests/Ornare.Imaging.Tests/FolderColorsTests.cs` |
| F02 | Recent custom colours, capped and persisted | `src/Ornare.Core/UserSettings.cs`, `SqliteStorageService` | `tests/Ornare.Imaging.Tests/RecentCustomColorsTests.cs` |
| F03 | Folder colour application end to end | `ApplyService`, `FolderCustomizer` | `tests/Ornare.IntegrationTests/FolderColorApplyIntegrationTests.cs` |
| F04 | Status / priority badge presets | `src/Ornare.Imaging/FolderTemplateRenderer.cs` | `tests/Ornare.Imaging.Tests/StatusPresetTests.cs`, `PriorityPresetTests.cs` |
| F05 | Optional emblem overlays | `PersonalizationStagingModel`, `FolderEmblemCompositor` | `tests/Ornare.Imaging.Tests/VectorEmblemTests.cs` |
| F06 | Image placement modes | `src/Ornare.Imaging/ImagePlacementMode.cs` | `tests/Ornare.Imaging.Tests/ImagePlacementTests.cs` |
| F07 | Padding and canvas fitting | `src/Ornare.Imaging/ImagePipeline.cs` | `tests/Ornare.Imaging.Tests/TransparentPaddingTests.cs` |
| F08 | Background compositing modes | `src/Ornare.Imaging/BackgroundMode.cs` | `tests/Ornare.Imaging.Tests/BackgroundColorTests.cs` |
| F09 | SVG recolouring | `src/Ornare.Imaging/SvgRecolorService.cs` | `tests/Ornare.Imaging.Tests/SvgRecoloringTests.cs` |
| F10 | Deterministic OKLab folder palettes | `src/Ornare.Imaging/FolderPaletteGenerator.cs` | `tests/Ornare.Imaging.Tests/FolderColorsTests.cs` |
| F12 | ICO import and preview | `src/Ornare.Imaging/IcoImportService.cs` | `tests/Ornare.Imaging.Tests/IcoImportTests.cs` |
| F13 | ICO export to a chosen path | `src/Ornare.Windows/AtomicExportWriter.cs` | `tests/Ornare.IntegrationTests/AtomicExportIntegrationTests.cs` |
| F14 | PNG export sizes | `src/Ornare.Imaging/PngExportService.cs` | `tests/Ornare.IntegrationTests/PngExportIntegrationTests.cs` |
| F15 | Library tags UI and storage | `ManagerWindow.xaml.cs`, `SqliteStorageService` | `tests/Ornare.Storage.Tests/LibraryTagsStorageTests.cs` |
| F16 | Custom collections | `ManagerWindow.xaml.cs`, `SqliteStorageService` | `tests/Ornare.Storage.Tests/CollectionStorageTests.cs` |
| F17 | Library search tokenizer | `src/Ornare.Core/LibrarySearchTokenizer.cs` | `tests/Ornare.Core.Tests/LibrarySearchTokenizerTests.cs` |
| F18 | Content-addressed render cache | `src/Ornare.Imaging/ImagePipeline.cs` | `tests/Ornare.Imaging.Tests/RenderCacheAndPipelineTests.cs` |
| F19 | NTFS identity reconciliation for moved folders | `src/Ornare.Storage/PortableFolder*.cs` | `tests/Ornare.IntegrationTests/PortableFolderReconciliationTests.cs` |
| F20 | Drop limit of 1000 items | `FolderStagingService` | `tests/Ornare.IntegrationTests/FolderStagingIntegrationTests.cs` |
| F21 | Last-applied render key tracking | `ApplyService`, `SqliteStorageService` | `tests/Ornare.IntegrationTests/ApplyLastIntegrationTests.cs` |
| F23 | Explorer context menu: three fixed commands | `src/Ornare.ShellExtension` | `tests/Ornare.UiTests/ShellExtensionFlyoutTests.cs` |
| F24 | Deterministic CLI with JSON envelope | `src/Ornare.Cli` | `tests/Ornare.IntegrationTests/CliExecutionIntegrationTests.cs` |
| F25 | Native AOT CLI, source-generated JSON | `src/Ornare.Cli/CliJsonContext.cs` | `tests/Ornare.IntegrationTests/CliGrammarValidationTests.cs` |
| F26 | Turkish as a first-class locale | `src/Ornare.App/AppResources.cs` | `tests/Ornare.UiTests/LocalizationParityTests.cs` |
| F27 | English as a first-class locale | `src/Ornare.App/AppResources.cs` | `tests/Ornare.UiTests/LocalizationParityTests.cs` |

## Cross-cutting requirements

| Requirement | Invariant | Verification |
|-------------|-----------|--------------|
| Crash-safe journal and reconciliation | INV-TX-001, INV-TX-002 | `FaultInjectionCrashRecoveryTests.cs`, `JournalStageContractTests.cs` |
| Encoding-preserving `desktop.ini` | INV-FS-002 | `DesktopIniEncodingTests.cs` |
| Per-folder single-operation lock | INV-CON-001 | `OperationConcurrencyIntegrationTests.cs`, `PerTargetSyncLockLifecycleTests.cs` |
| Single manager window | INV-CON-003 | `ReentrancyScopeTests.cs` |
| Settings atomicity | INV-DATA-001 | `SqliteStorageServiceTests.cs` |
| No full local paths in cloud snapshots | INV-DATA-003 | `CloudSyncServiceTests.cs` |
| Localized user copy vs. diagnostic detail | INV-DATA-004, INV-UI-004 | `LocalizationParityTests.cs`, CLI integration tests |
| No global Shell invalidation | INV-CON-004 | `ShellRefreshContractTests.cs` |
