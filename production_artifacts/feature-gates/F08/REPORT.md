# Feature Gate Report: F08 — Background Color

**Feature ID:** F08  
**Specification:** `feature-expansion/features/F08_background-color.md`  
**Execution Order:** 8 of 27  
**Traceability ID:** `ORN-EXP-F08`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Completion Date:** 2026-09-26  

---

## 1. Executive Summary

Feature F08 adds solid background color compositing for custom folder icon assets without mutating the original imported files. A transparent source image/logo can be placed over a chosen solid background color (`#RRGGBB`), rendered through sRGB normalization and Magick.NET's native Porter-Duff `CompositeOperator.Over` operator across all 10 multi-resolution ICO frames (16px to 256px), with a fast 256x256 PNG preview for the Quick Panel. The UI reuses the existing native WinUI 3 `ColorPicker` component with an 80 ms debounced preview loop, ensuring that zero multi-frame ICO files are generated during interactive color adjustment.

---

## 2. Invariants & Gates Verification

| Invariant | Description | Verification Evidence | Status |
|---|---|---|---|
| `INV-EXP-BG-01` | BackgroundMode defaults to Transparent (`0`); SolidColor is opaque `#RRGGBB` | Tested in `RecipeDefaultsToTransparentModeInvExpBg01` and `BackgroundSpecSolidNormalizesHexValues`. | **PASS** |
| `INV-EXP-BG-02` | Transparent mode ignores stored color during canonical serialization and cache key calculation | Tested in `RecipeTransparentModeIgnoresStoredColorInCanonicalSerializationInvExpBg02`. Changing color while in Transparent mode produces byte-for-byte identical canonical serialization and render key. | **PASS** |
| `INV-EXP-BG-03` | Foreground alpha and anti-aliased edges are preserved through sRGB normalization and composition; no edge fringing | Tested in `AlphaAntiAliasingSmoothTransitionWithoutHalosInvExpBg03`. Semi-transparent pixels (alpha 128) blend smoothly with background without black or white fringing. | **PASS** |
| `INV-EXP-BG-04` | Source image asset bytes are strictly immutable; background exists solely in recipe and derived render output | Tested in `SourceAssetBytesStrictlyImmutableInvExpBg04`. SHA-256 hash of original input bytes before and after processing/previewing remains 100% identical. | **PASS** |
| `INV-EXP-BG-05` | Reuses the WinUI ColorPicker component; no duplicate color selection engine | In `QuickPanelWindow.xaml`, `BackgroundColorPicker` reuses the native WinUI 3 `ColorPicker` control with `IsAlphaEnabled="False"`. | **PASS** |
| `INV-EXP-BG-PERF-01` | Single composite layer; zero measurable overhead versus normal rendering | Single-pass raster compositing directly onto canvas; 127 imaging tests pass within budget. | **PASS** |

---

## 3. Implementation Details

### A. Core Architecture (`src/Ornare.Core/`)
- Created `BackgroundMode.cs`: Enum defining `Transparent = 0` and `SolidColor = 1`.
- Created `BackgroundSpec.cs`: Readonly record struct with factory methods (`Transparent`, `Solid`) and `NormalizeHex` / `IsValidHex` helpers.
- Updated `FolderJsonContext.cs`: Registered `BackgroundMode` and `BackgroundSpec` for Native AOT source generation.
- Updated `RenderRecipe.cs`:
  - Added `Background => (BackgroundMode)BackgroundMode;`.
  - Canonical serialization enforces `INV-EXP-BG-02`: if `BackgroundMode == Transparent`, writes `string.Empty`. If `SolidColor`, writes normalized uppercase `#RRGGBB`.
  - Deserialization strictly validates `BackgroundMode` (must be 0 or 1), throwing `InvalidDataException` on corrupt input.

### B. Imaging Engine (`src/Ornare.Imaging/`)
- Updated `IImagePipeline.cs` and `ImagePipeline.cs`:
  - Added `BackgroundMode backgroundMode = BackgroundMode.Transparent, string? backgroundColorHex = null` to `ProcessImage`, `ProcessImageBytes`, `RenderImageBytesCore`, and `RenderPlacementPreviewPng`.
  - In `RenderImageBytesCore` and `RenderPlacementPreviewPng`: Canvas is initialized with `canvasBg = backgroundMode == SolidColor ? new MagickColor(normalizedHex) : MagickColors.Transparent`.
  - Sub-images are composited with `CompositeOperator.Over` in sRGB color space.
- Configured ImageMagick security and SVG delegate policy in `ImageSecurityPolicy.cs`:
  - Set `<policy domain="delegate" rights="none" pattern="*" />` to block external process spawning (e.g. `inkscape`) during SVG rasterization.
  - Explicitly specified `BackgroundColor = MagickColors.Transparent` on all SVG `MagickReadSettings`.

### C. Quick Panel UI (`src/Ornare.App/`)
- Extended `PlacementOptionsFlyout` in `QuickPanelWindow.xaml`:
  - Added Background section with `BackgroundModeRadioButtons` (`Transparent` vs `Solid Color`).
  - Added color swatch button that opens `BackgroundColorPickerFlyout` housing a WinUI 3 `ColorPicker` (`IsAlphaEnabled="False"`).
- Updated `QuickPanelWindow.xaml.cs`:
  - Added `_stagedBackgroundMode` and `_stagedBackgroundColorHex` fields.
  - Implemented `OnBackgroundSelectionChanged` and `OnBackgroundColorPickerColorChanged`.
  - Extended `TriggerDebouncedPreview(int? paddingPercent = null)` with an 80 ms `CancellationTokenSource` debounce. Zero multi-frame ICO files are generated during color picker interaction; preview uses bounded 256x256 PNGs.
- Localized in `en-US` and `tr-TR` `Resources.resw` and dynamically switched in `UpdatePlacementStrings`.

---

## 4. Verification Evidence

### Test Suite Execution
- `Ornare.Imaging.Tests`: 127 / 127 passed (15 new dedicated tests in `BackgroundColorTests.cs`).
- `Ornare.Core.Tests`: 26 / 26 passed.
- `Ornare.Storage.Tests`: 21 / 21 passed.
- `Ornare.UiTests`: 9 / 9 passed.
- `Ornare.IntegrationTests`: 34 / 34 passed.
- **Total Test Count:** 217 passed, 0 failed.

### Cross-Architecture ARM64 Verification
- `scripts/build-managed-arm64.ps1`:
  - `src/Ornare.Core/Ornare.Core.csproj`: Clean (0 errors, 0 warnings).
  - `src/Ornare.DesignSystem/Ornare.DesignSystem.csproj`: Clean (0 errors, 0 warnings).
  - `src/Ornare.Windows/Ornare.Windows.csproj`: Clean (0 errors, 0 warnings).
  - `src/Ornare.Storage/Ornare.Storage.csproj`: Clean (0 errors, 0 warnings).
  - `src/Ornare.Imaging/Ornare.Imaging.csproj`: Clean (0 errors, 0 warnings).
  - `src/Ornare.App/Ornare.App.csproj`: Clean (0 errors, 0 warnings).

### Real Golden Path Verification
- `pwsh -File scripts/test-real-golden-path.ps1`: PASSED.
  - Step 1 to 8 completed cleanly.
  - Preserved unrelated `[ViewState]`, `[HarmlessEnterpriseMetadata]`, and `InfoTip`.
  - Shell refresh via `SHChangeNotify` with 0 Explorer restarts/taskkill.

---

## 5. Artifacts & Code Check

- `src/Ornare.Core/BackgroundMode.cs`
- `src/Ornare.Core/BackgroundSpec.cs`
- `src/Ornare.Core/FolderJsonContext.cs`
- `src/Ornare.Core/RenderRecipe.cs`
- `src/Ornare.Imaging/IImagePipeline.cs`
- `src/Ornare.Imaging/ImagePipeline.cs`
- `src/Ornare.Imaging/ImageSecurityPolicy.cs`
- `src/Ornare.Imaging/FolderTemplateRenderer.cs`
- `src/Ornare.App/QuickPanelWindow.xaml`
- `src/Ornare.App/QuickPanelWindow.xaml.cs`
- `src/Ornare.App/Strings/en-US/Resources.resw`
- `src/Ornare.App/Strings/tr-TR/Resources.resw`
- `tests/Ornare.Imaging.Tests/BackgroundColorTests.cs`
- `docs/REQUIREMENTS_TRACEABILITY.md`
- `production_artifacts/feature-gates/F08/PREFLIGHT.md`
- `production_artifacts/feature-gates/F08/REPORT.md`
