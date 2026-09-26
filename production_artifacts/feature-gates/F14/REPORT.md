# Feature Gate Report: F14 — PNG Export

**Feature ID:** F14  
**Feature Name:** PNG Export  
**Specification:** `feature-expansion/features/F14_png-export.md`  
**Execution Order:** 14 of 27  
**Traceability ID:** `ORN-EXP-F14`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Completion Date:** 2026-09-26  

---

## 1. Executive Summary

Feature F14 delivers an on-demand, high-fidelity PNG export pipeline for Ornare. Users can export customized folder icons and library assets directly as standalone PNG images at exact square resolutions:
- **1024 × 1024 px** (Default, master resolution)
- **512 × 512 px** (High-DPI / web asset)
- **256 × 256 px** (Standard icon preview)

Key architectural highlights:
1. **Direct Vector Rasterization at Destination Resolution:** Rather than upscaling a 256px raster frame (which would introduce bicubic/bilinear blur and edge artifacts), `PngExportService` adjusts root SVG dimensions and rasterization density (`Density = 96.0 * targetSize / 256.0`). Badges and multi-layer folder templates are rasterized directly at 512px or 1024px from vector paths, guaranteeing razor-sharp edges and pristine vector fidelity.
2. **Transparent vs Solid Background Modes:** If the customized asset or recipe defines an explicit background color (`F08`), the canvas is rendered with that solid background color. If no background color is configured, a 32-bit RGBA canvas with true alpha transparency is exported.
3. **F18 Render Cache Integration:** Rendered PNG bytes are cached deterministically under `{renderKey}.png` within Ornare's local app data cache. Subsequent requests for the same recipe and dimension bypass re-rendering and are copied directly.
4. **Physical Atomicity & Non-Destructive Semantics:** Export writes are handled through `AtomicExportWriter.ExportPngAtomically`, creating a sibling temporary file (`.ornare-export-{GUID}.tmp`), flushing to physical storage (`Flush(flushToDisk: true)`), verifying PNG container headers (`0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A`) and IHDR width/height, and committing atomically via `ReplaceFileW` / `MoveFileExW(MOVEFILE_WRITE_THROUGH)`.
5. **Manager UI Context Menu:** Added a flyout submenu `Export as PNG` under `LibraryGridView` items with `1024 × 1024 (Default)`, `512 × 512`, and `256 × 256` options. Fully localized in English (`en-US`) and Turkish (`tr-TR`).

---

## 2. Invariants & Gates Verification

| Invariant / Decision | Description | Verification Evidence | Status |
|---|---|---|---|
| `F14-DEC-01` | Supported dimensions: exactly 256, 512, 1024 square. All other dimensions rejected before rendering | Verified in `ExportPngRejectsInvalidDimensions`. | **PASS** |
| `F14-DEC-02` | Native vector rasterization at target size; no upscaling blur | Verified in `ExportPngDirectVectorRasterizationSharpness` and `PngExportIntegrationTests`. | **PASS** |
| `F14-DEC-03` | Transparent background by default; solid background when recipe specifies `F08` | Verified in `ExportPngPreservesAlphaTransparency` and `ExportPngAppliesSolidBackground`. | **PASS** |
| `F14-DEC-04` | 256x256 PNG is visually and color-identical to Manager/Quick Panel preview | Verified in `ExportPng256VisualMatchWithPreview`. | **PASS** |
| `F14-DEC-05` | Sibling temporary file (`.ornare-export-{GUID}.tmp`), flushed to disk, validated, committed with `ReplaceFileW` / `MoveFileExW` | Verified in `ExportPngExistingDestinationReplacedAtomically`. | **PASS** |
| `F14-DEC-06` | Injected failure leaves pre-existing destination file 100% byte-for-byte intact with zero temp leakage | Verified in `ExportPngInjectedFailurePreservesOriginalDestination`. | **PASS** |
| `F14-DEC-07` | Cache hit reuse (`{renderKey}.png`); identical bytes and execution speedup | Verified in `ExportPngCacheHitMatchesByteForByte`. | **PASS** |

---

## 3. Implementation Details

### A. Core Models (`src/Ornare.Core/`)
- Created `ExportPngModels.cs`:
  - `ExportPngConstants`: Supported frame sizes (`256, 512, 1024`), default size (`1024`).
  - `ExportPngRequest`: `SourceAssetPath`, `DestinationPath`, `TargetSize`, `RenderKey`, `Recipe`.
  - `ExportPngResult`: `DestinationPath`, `FileSizeBytes`, `Width`, `Height`, `IsFromCache`.
  - `RenderPngResult`: `PngBytes`, `Width`, `Height`, `RenderKey`.

### B. Imaging Pipeline (`src/Ornare.Imaging/`)
- Created `PngExportService.cs`:
  - Implements vector SVG density recalculation (`AdjustSvgDimensions`) to dynamically generate crisp multi-layer vector rasters at 512px and 1024px.
  - Handles raster image scaling with high-quality cubic filtering.
  - Implements transparent and solid background canvas compositing.
  - Manages PNG render cache lookup and storage.
- Updated `FolderTemplateRenderer.cs`: Added `RenderTemplatePng` and `RenderPresetPng`.
- Updated `StatusBadgeCompositor.cs`: Added direct vector rasterization for frame sizes $> 256\text{px}$.
- Updated `IImagePipeline.cs` and `ImagePipeline.cs`: Exposed `RenderPng` and `RenderPngWithCache`.

### C. Windows Native Exporter (`src/Ornare.Windows/`)
- Updated `AtomicExportWriter.cs`:
  - Extended atomic export logic to support PNG files.
  - Added `ValidatePngBasic` and `TryGetPngInfo` to verify 8-byte PNG signature and read big-endian IHDR width/height chunks before committing to disk.
  - Implemented `ExportPngAtomically` and `ExportPngFileAtomically`.

### D. UI & Localization (`src/Ornare.App/`)
- Updated `ManagerWindow.xaml`: Added `ContextExportPngSubItem` with 1024x1024, 512x512, 256x256 menu flyout items.
- Updated `ManagerWindow.xaml.cs`: Implemented `OnExportPngClicked` with HWND-initialized `FileSavePicker` and asynchronous export.
- Updated `Resources.resw` (en-US and tr-TR) with all localized labels.

---

## 4. Test & Verification Evidence

- **Integration Test Suite (`tests/Ornare.IntegrationTests/PngExportIntegrationTests.cs`):**
  - `ExportPngRendersAllSupportedSizes`: PASS (256, 512, 1024).
  - `ExportPngPreservesAlphaTransparency`: PASS (Alpha = 0 on corners).
  - `ExportPngAppliesSolidBackground`: PASS (Exact solid RGB fill).
  - `ExportPng256VisualMatchWithPreview`: PASS (Color match with preview).
  - `ExportPngExistingDestinationReplacedAtomically`: PASS.
  - `ExportPngInjectedFailurePreservesOriginalDestination`: PASS.
  - `ExportPngCacheHitMatchesByteForByte`: PASS.
  - `ExportPngRejectsInvalidDimensions`: PASS (48px, 128px, 2048px rejected).
- **Core Tests:** All 26 tests PASS.
- **Integration Tests:** All 49 tests PASS.

---

## 5. Traceability Compliance

- Requirement `ORN-EXP-F14` registered in `docs/REQUIREMENTS_TRACEABILITY.md`.
- Zero placeholders; zero script-generated code; zero breaking changes.
- Ready for local commit and progression to Feature 15 (F15 — Library Tags).
