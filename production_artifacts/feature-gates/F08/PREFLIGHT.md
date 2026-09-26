# Feature Preflight: F08 — Background Color

**Feature ID:** F08  
**Specification:** `feature-expansion/features/F08_background-color.md`  
**Execution Order:** 8 of 27  
**Traceability ID:** `ORN-EXP-F08`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  

---

## 1. Live Implementation Points

- **`src/Ornare.Core/`:**
  - Create `BackgroundMode.cs` enum: `Transparent = 0`, `SolidColor = 1`.
  - Create `BackgroundSpec.cs` readonly record struct with `NormalizeHex` helper.
  - Update `RenderRecipe.cs`:
    - Add `Background => (BackgroundMode)BackgroundMode;`.
    - Ensure `CanonicalRecipeSerializer.Serialize` canonicalizes: if `BackgroundMode == 0` (Transparent), stored color is ignored and serialized as empty string. If `SolidColor`, color is serialized as normalized `#RRGGBB`.
  - Register `BackgroundMode` and `BackgroundSpec` in `FolderJsonContext.cs` for reflection-free AOT serialization.
- **`src/Ornare.Imaging/`:**
  - Update `ImagePipeline.cs` `RenderImageBytesCore` and `RenderPlacementPreviewPng`:
    - When `backgroundMode == BackgroundMode.SolidColor` and `backgroundColorHex` is provided:
      Create base frame canvas with `new MagickColor(backgroundColorHex)` instead of `MagickColors.Transparent`.
      Composite normalized foreground image over the solid background with `CompositeOperator.Over`.
    - Preserve alpha anti-aliasing and prevent halo/fringing artifacts.
    - Source image asset bytes remain strictly immutable.
- **`src/Ornare.App/`:**
  - Extend `PlacementOptionsFlyout` in `QuickPanelWindow.xaml`:
    - Background section: Segmented / RadioButtons or ComboBox for `Transparent` vs `Solid Color`.
    - When `Solid Color` is selected, display ColorPicker / Color button reusing the F02 ColorPicker component.
    - 80 ms debounced preview rendering; zero ICO files generated during color adjustment.
    - Bilingual English (`en-US`) and Turkish (`tr-TR`) resources.
- **`tests/`:**
  - Unit tests for `BackgroundSpec` and canonical recipe serialization.
  - Composition tests with transparent PNGs over Transparent, White, Black, and Vibrant saturated backgrounds.
  - Alpha preservation and anti-aliasing edge checks (no dark halos).
  - Cache key divergence: different backgrounds produce distinct keys; changing color while Transparent does not alter cache key.
  - Original source asset immutability verification.

---

## 2. Invariants & Gates

- `INV-EXP-BG-01`: BackgroundMode defaults to Transparent (`0`); SolidColor is opaque `#RRGGBB`.
- `INV-EXP-BG-02`: Transparent mode ignores stored color during canonical serialization and cache key calculation.
- `INV-EXP-BG-03`: Foreground alpha and anti-aliased edges are preserved through sRGB normalization and composition; no edge fringing.
- `INV-EXP-BG-04`: Source image asset bytes are strictly immutable; background exists solely in recipe and derived render output.
- `INV-EXP-BG-05`: Reuses the WinUI ColorPicker component; no duplicate color selection engine.
- `INV-EXP-BG-PERF-01`: Single composite layer; zero measurable overhead versus normal rendering.

---

## 3. Supply Chain & Provenance Decisions

- Pure Magick.NET raster compositing using `CompositeOperator.Over`.
- Reuses WinUI 3 `ColorPicker` from Windows App SDK.
