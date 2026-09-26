# Feature Preflight: F07 — Transparent Padding

**Feature ID:** F07  
**Specification:** `feature-expansion/features/F07_transparent-padding.md`  
**Execution Order:** 7 of 27  
**Traceability ID:** `ORN-EXP-F07`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  

---

## 1. Live Implementation Points

- **`src/Ornare.Core/RenderRecipe.cs`:**
  - `PaddingPercent` property is already present in `RenderRecipe` and canonical binary serializer.
  - Validate `PaddingPercent` range to [0, 30]. Any negative value or value > 30 is rejected during deserialization/validation.
  - Off state canonically serializes as `PaddingPercent = 0`.
  - Canonicalize 0% identically to Off for render cache identity.
- **`src/Ornare.Imaging/ImagePlacementCalculator.cs`:**
  - Authoritative inset calculation:
    `padX = (int)Math.Round(canvasW * (paddingPercent / 100.0), MidpointRounding.AwayFromZero)`.
    Or per-side padding: `paddingPercent` is percentage per side (0–30%).
    Inner usable width = `canvasW - 2 * padX`, inner usable height = `canvasH - 2 * padY`.
    Offset X = `padX`, offset Y = `padY`.
  - Feed inner usable rectangle to Contain/Cover/Fill placement math.
  - Consistent midpoint rounding policy (`MidpointRounding.AwayFromZero`) tested across all 10 standard ICO frame sizes (16, 20, 24, 32, 40, 48, 64, 96, 128, 256).
- **`src/Ornare.Imaging/IImagePipeline.cs` and `ImagePipeline.cs`:**
  - Support `int paddingPercent = 0` across `ProcessImage`, `ProcessImageBytes`, and `RenderPlacementPreviewPng`.
  - Preview PNG rendering and ICO frame rendering share the exact same rounding and geometry calculator.
- **`src/Ornare.App/QuickPanelWindow.xaml` and `.xaml.cs`:**
  - In `PlacementOptionsFlyout`:
    - Add `ToggleSwitch` for `Padding` (defaults to Off).
    - When turned On, enables `PaddingSlider` initialized to 8% (range 0–30%, step 1).
    - Displays current percentage text label (`8%`).
    - Moving slider cancels previous debounced preview tasks via `CancellationTokenSource` and renders 256x256 preview PNG using `ImagePipeline.RenderPlacementPreviewPng`.
    - **Never** rerenders full ICO on slider movement. Full ICO is rendered only upon commit/apply.
    - Full keyboard accessibility and UI Automation name.
    - Bilingual English and Turkish localization in `Resources.resw`.
- **`tests/`:**
  - Geometry tests for 0%, 8%, 30% padding across all 10 target frame sizes.
  - Validation tests rejecting invalid negative and > 30% values.
  - Deterministic render cache key tests proving distinct keys when padding changes.
  - Visual preview PNG equivalence tests with final 256px frame.

---

## 2. Invariants & Gates

- `INV-EXP-PAD-01`: Padding defaults to Off (`PaddingPercent = 0`). Turning On defaults to 8% per side. Range is hard-capped at [0, 30]%.
- `INV-EXP-PAD-02`: A user-selected 0% is canonicalized identically to Off (`PaddingPercent = 0`) for render and cache identity.
- `INV-EXP-PAD-03`: Padding applies before Contain/Cover/Fill geometry; Cover fills the inner padded rectangle, not the outer canvas.
- `INV-EXP-PAD-04`: Midpoint rounding policy is consistent across all 10 ICO frame sizes.
- `INV-EXP-PAD-05`: Sliders update preview only; full ICO is never rerendered on slider movement.
- `INV-EXP-PAD-06`: Recipe serialization includes `PaddingPercent`, producing deterministic cache keys.
- `INV-EXP-PAD-PERF-01`: Pure integer/double math; zero heap allocations during geometry calculation.

---

## 3. Supply Chain & Provenance Decisions

- Pure mathematical calculation in `ImagePlacementCalculator.cs`.
- No new external libraries or dependencies.
