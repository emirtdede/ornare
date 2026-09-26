# Feature Preflight: F06 — Contain / Cover / Fill

**Feature ID:** F06  
**Specification:** `feature-expansion/features/F06_contain-cover-fill.md`  
**Execution Order:** 6 of 27  
**Traceability ID:** `ORN-EXP-F06`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  

---

## 1. Live Implementation Points

- **`src/Ornare.Core/ImagePlacementMode.cs`:**
  - Define `ImagePlacementMode` enum (`Contain = 0`, `Cover = 1`, `Fill = 2`).
  - Strict default is `Contain`.
  - Add typed accessor to `RenderRecipe`.
- **`src/Ornare.Imaging/ImagePlacementCalculator.cs`:**
  - Pure, allocation-free geometry engine:
    `ComputePlacement(int sourceWidth, int sourceHeight, int canvasWidth, int canvasHeight, ImagePlacementMode mode, double paddingPercent = 0.0)`.
  - Returns `readonly struct PlacementGeometry`:
    - `SourceCropX`, `SourceCropY`, `SourceCropWidth`, `SourceCropHeight`
    - `DestX`, `DestY`, `DestWidth`, `DestHeight`
    - `CanvasWidth`, `CanvasHeight`
  - Zero heap allocation per call.
  - Mathematical precision:
    - `Contain`: `scale = min(usableW / srcW, usableH / srcH)`. Preserves aspect ratio with letterboxing.
    - `Cover`: `scale = max(usableW / srcW, usableH / srcH)`. Center-crops source image, preserves aspect ratio.
    - `Fill`: `scaleX = usableW / srcW`, `scaleY = usableH / srcH`. Stretches to fill canvas. Explicitly opt-in only.
- **`src/Ornare.Imaging/ImagePipeline.cs`:**
  - Update `ProcessImageBytes` and `ProcessRecipe` to accept `ImagePlacementMode`.
  - Decode source image once; construct master canvas and ICO frames using `ImagePlacementCalculator`.
  - Never resize a previously generated frame (each frame rendered from normalized master).
- **`src/Ornare.App/` & `ManagerWindow.xaml` / Editor:**
  - Support placement mode selection in advanced image workflow / manager editor without cluttering the primary minimal Quick Panel.
  - Bilingual string resources (`Contain`, `Cover`, `Fill`, with explicit distortion notice on `Fill`).
- **`tests/`:**
  - Pure geometry unit tests for landscape, portrait, square across all 3 modes and boundary conditions (1px, extreme aspect ratios).
  - Golden image render tests proving pixel-perfect aspect ratio preservation on Contain/Cover and expected stretch on Fill.
  - Cache key divergence test: different modes produce distinct `RenderKey`s for identical source assets.
  - Performance benchmark proving zero-allocation / sub-microsecond placement computation.

---

## 2. Invariants & Gates

- `INV-EXP-PLACE-01`: Contain is the default placement mode (`PlacementMode = 0`) preserving aspect ratio with letterboxing.
- `INV-EXP-PLACE-02`: Cover preserves aspect ratio and center-crops to fill the canvas.
- `INV-EXP-PLACE-03`: Fill stretches with independent X/Y scaling; opt-in only, never default.
- `INV-EXP-PLACE-04`: Placement mode is serialized in `RenderRecipe` and differentiates the render cache key.
- `INV-EXP-PLACE-05`: Identical placement geometry used for preview and all 10 ICO frames.
- `INV-EXP-PLACE-PERF-01`: Geometry calculation is allocation-free; source decoded once per pipeline execution.

---

## 3. Supply Chain & Provenance Decisions

- Pure vector/raster math in `ImagePlacementCalculator.cs` (no third-party geometry libraries).
- Uses existing pinned Magick.NET for raster transformations.

---

## 4. Preflight Baseline

- All 152 solution tests currently green.
- x64 and ARM64 Release builds green with 0 errors, 0 warnings.
- F18, F09, F10, F01, F02 gates green.
