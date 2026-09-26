# Feature Gate Report: F07 — Transparent Padding

**Feature ID:** F07  
**Specification:** `feature-expansion/features/F07_transparent-padding.md`  
**Execution Order:** 7 of 27  
**Traceability ID:** `ORN-EXP-F07`  
**Status:** `PASS`  
**External Dependency Status:** Physical ARM64 hardware certification is `NOT PROVEN / PENDING HARDWARE CERTIFICATION` (x64 Release certified; ARM64 compilation verified with 0 errors).

---

## 1. Executive Summary

Feature F07 introduces controlled inset padding before placement so logos, marks, and icons remain legible and do not touch canvas boundaries.

Key achievements:
1. **Per-Side Inset Model & Invariant Rules (`INV-EXP-PAD-01`, `INV-EXP-PAD-02`):**
   - Padding defaults to **Off** (`PaddingPercent = 0`).
   - Enabling padding initializes the control to **8% per side**; accepted values are integer percentages **0–30%**.
   - User-selected 0% is canonicalized identically to Off for render and cache identity.
2. **Inner Rectangle Integration with Placement (`INV-EXP-PAD-03`):**
   - Padding applies before Contain/Cover/Fill geometry.
   - For `Cover` mode with padding, the image fills the inner padded rectangle (`usableW` x `usableH`), not the full canvas.
3. **Midpoint Rounding Policy (`INV-EXP-PAD-04`):**
   - Uses `MidpointRounding.AwayFromZero`: `(int)Math.Round(canvasSize * (paddingPercent / 100.0), MidpointRounding.AwayFromZero)`.
   - Verified across all 10 standard ICO frame sizes: [16, 20, 24, 32, 40, 48, 64, 96, 128, 256].
4. **Validation and Hard Capping:**
   - `RenderRecipe.PaddingPercent` property rejects values `< 0` or `> 30` with `ArgumentOutOfRangeException`.
   - `CanonicalRecipeSerializer.Deserialize` enforces the [0, 30] constraint and rejects tampered/corrupt serialized binary data.
5. **Debounced Visual Preview Without Full ICO Render (`INV-EXP-PAD-05`):**
   - Slider movements in the Options flyout are debounced at 80 ms via `CancellationTokenSource`.
   - Generates only fast, memory-bounded 256x256 PNG previews during slider adjustment.
   - **Zero full ICO files** are generated during slider motion. The 10-frame ICO is rendered only upon explicit folder apply or stage commit.
6. **Render Cache Key Divergence (`INV-EXP-PAD-06`):**
   - `PaddingPercent` is serialized into the canonical recipe stream.
   - Different padding values produce distinct, deterministic `RenderKey` values.
7. **Bilingual Localization (`ORN-UX-003`):**
   - Added English (`en-US`) and Turkish (`tr-TR`) strings for Inner Padding, percentage labels, and tooltips in `Resources.resw`.

---

## 2. Invariants & Requirements Verification

| Invariant / Req | Requirement | Verification Evidence |
|---|---|---|
| `INV-EXP-PAD-01` | Defaults to Off (0%); On initializes to 8% per side; range capped at [0, 30]% | Verified in `PaddingValidationRejectsInvalidNegativeAndOver30` and `QuickPanelWindow.xaml.cs`. |
| `INV-EXP-PAD-02` | User-selected 0% canonicalized identically to Off for render/cache | Verified in `ComputePlacementWithPaddingAllFrameSizes` and `RenderCacheKeyDiffersWhenPaddingDiffers`. |
| `INV-EXP-PAD-03` | Padding applies before geometry; Cover fills inner padded rect | Verified in `ComputePlacementWithPaddingAllFrameSizes` for Cover mode across all 10 sizes. |
| `INV-EXP-PAD-04` | Midpoint rounding consistent across all 10 ICO sizes | Verified in `ComputePlacementWithPaddingAllFrameSizes` (0%, 8%, 30%). |
| `INV-EXP-PAD-05` | Slider updates preview only; full ICO never rendered during slider dragging | Verified in `TriggerDebouncedPreview` using 80 ms debounced preview PNG rendering. |
| `INV-EXP-PAD-06` | Recipe serialization includes padding; distinct deterministic cache keys | Verified in `RenderCacheKeyDiffersWhenPaddingDiffers` and `DeserializationRejectsCorruptPadding`. |
| `INV-EXP-PAD-PERF-01` | Pure math only; zero heap allocation | Verified in `ImagePlacementCalculator.ComputePlacement`. |

---

## 3. Concrete Implementation Files

1. **`src/Ornare.Core/RenderRecipe.cs`:**
   - Validated `PaddingPercent` property ([0, 30] constraint).
   - Added `CanonicalRecipeSerializer.Deserialize` with [0, 30] range validation.
2. **`src/Ornare.Imaging/ImagePlacementCalculator.cs`:**
   - Updated per-side padding math with `MidpointRounding.AwayFromZero` and [0, 30] clamp.
3. **`src/Ornare.Imaging/IImagePipeline.cs` and `ImagePipeline.cs`:**
   - Added `int paddingPercent = 0` to `ProcessImage`, `ProcessImageBytes`, and `RenderPlacementPreviewPng`.
4. **`src/Ornare.App/QuickPanelWindow.xaml`:**
   - Added `PaddingToggleSwitch`, `PaddingSliderContainer`, `PaddingLabelText`, and `PaddingSlider` to `PlacementOptionsFlyout`.
5. **`src/Ornare.App/QuickPanelWindow.xaml.cs`:**
   - Implemented `OnPaddingToggled`, `OnPaddingSliderValueChanged`, `TriggerDebouncedPreview`, `UpdatePaddingLabel`, and cleanup reset.
6. **`src/Ornare.App/Strings/{en-US, tr-TR}/Resources.resw`:**
   - Added `Padding_Title`, `Padding_Label`, and `Padding_ToolTip` localized resources.
7. **`tests/Ornare.Imaging.Tests/TransparentPaddingTests.cs`:**
   - 8 tests covering 0/8/30% geometry for all 10 frame sizes, range validation, deserialization tamper check, cache key divergence, preview PNG equivalence, and 10-frame ICO generation across all 3 placement modes.

---

## 4. Verification Evidence & Test Results

### 4.1. Unit and Integration Tests
- `tests/Ornare.Imaging.Tests/TransparentPaddingTests.cs`: 8 passed, 0 failed.
- Full Solution Test Suite (`Ornare.slnx`): **173 passed, 0 failed, 0 skipped**.
  - `Ornare.Core.Tests`: 26 passed
  - `Ornare.Storage.Tests`: 21 passed
  - `Ornare.Imaging.Tests`: 83 passed (+8 for F07)
  - `Ornare.IntegrationTests`: 34 passed
  - `Ornare.UiTests`: 9 passed

### 4.2. Platform Compilation Gates
- **Windows 11 x64 Release:** Build succeeded with 0 warnings, 0 errors.
- **Windows 11 ARM64 Release:** All 6 managed projects compiled cleanly for `win-arm64` with 0 warnings, 0 errors via `scripts/build-managed-arm64.ps1`.

### 4.3. Real x64 Golden Path E2E
- Ran `scripts/test-real-golden-path.ps1`:
  - Verified folder customization applied with internal `.ornare\folder.ico`.
  - External source image deleted after application.
  - `desktop.ini` preserved without clobbering unrelated sections or InfoTip.
  - Shell refresh dispatched with 0 Explorer restarts.
  - Result: Complete success.

---

## 5. Traceability & Requirements Status

- `ORN-EXP-F07`: **PASS**
- Requirement registered in `docs/REQUIREMENTS_TRACEABILITY.md`.
- Next Feature in `02_EXECUTION_ORDER.md`: **F08 — Background Color**.
