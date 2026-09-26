# Feature Gate Report: F10 — Adaptive Contrast / Palette Generation

**Feature ID:** F10  
**Specification:** `feature-expansion/features/F10_adaptive-contrast.md`  
**Execution Order:** 3 of 27  
**Traceability ID:** `ORN-EXP-F10`  
**Status:** `PASS`  
**External Dependency Status:** Physical ARM64 hardware certification is `NOT PROVEN / PENDING HARDWARE CERTIFICATION` (x64 Release certified; ARM64 compilation verified with 0 errors).

---

## 1. Executive Summary

Feature F10 establishes deterministic, high-fidelity 5-tone folder palette generation from a single user-selected sRGB base color using the modern OKLab/OKLCH color space (Björn Ottosson, 2020). The implementation:
1. **Preserves Exact Base Color:** The user-selected base color is never altered or truncated; it is preserved byte-for-byte in all storage, UI, recipe identity, and semantic rendering contexts (`INV-EXP-PALETTE-01`).
2. **Pure-Math OKLab / OKLCH Engine:** Implements high-precision, zero-allocation conversions between sRGB, linear sRGB, OKLab, and cylindrical OKLCH in `Ornare.Imaging` without adding external color-science dependencies.
3. **Deterministic Tonal Lightness Offsets:** Derives preferred lightness targets from base `L`:
   - `deep = L - 0.18`
   - `shadow = L - 0.11`
   - `light = L + 0.08`
   - `highlight = L + 0.14`
   Clamps only derived tone lightness to `[0.02, 0.98]`. Universal minimum spacing is rejected to avoid altering user base colors near black/white extremes; boundary tone collapse is fully handled and verified (`INV-EXP-PALETTE-03`).
4. **Monotonic Binary Search Gamut Mapping:** Out-of-gamut derived tones reduce chroma monotonically by binary search (max 24 iterations, epsilon `1e-7`) preserving hue. Channels are never clipped as a first-pass gamut strategy (`INV-EXP-PALETTE-02`).
5. **Neutral Tone Handling:** When base chroma is `< 1e-4`, hue is treated as undefined and neutral derived tones (`C = 0`) are generated at the target lightnesses (`INV-EXP-PALETTE-04`).
6. **WCAG Non-Text Contrast Compliance:** Computes relative luminance and contrast ratio according to WCAG 2.2 Section 1.4.11 (3:1 minimum for non-text graphical indicators). Selects black or white foreground by highest contrast ratio, with circular backing disk fallback when contrast is under threshold (`INV-EXP-PALETTE-05`).

---

## 2. Invariants & Requirements Verification

| Invariant | Requirement | Verification Evidence |
|---|---|---|
| `INV-EXP-PALETTE-01` | Base RGB value is never altered | Verified on all 256 gray levels + representative vibrant accents in `BaseColorIsStrictlyPreservedInAllPalettes`. |
| `INV-EXP-PALETTE-02` | Monotonic chroma reduction via binary search (no first-pass clipping) | Implemented in `ColorConversion.MapLchToRgb` with max 24 iterations and epsilon `1e-7`. |
| `INV-EXP-PALETTE-03` | Derived lightness offsets (-0.18, -0.11, +0.08, +0.14) clamped to [0.02, 0.98] | Verified on pure black, pure white, and 10,000 deterministic inputs in `AdaptiveContrastTests`. |
| `INV-EXP-PALETTE-04` | Neutral tone fallback when chroma < 1e-4 | Tested across neutral grayscale spectrum; hue treated as undefined, C = 0. |
| `INV-EXP-PALETTE-05` | WCAG non-text contrast (≥ 3:1) for graphical badges with backing fallback | Verified in `WcagContrastCalculationsMatchKnownValues` and `CircularBackingRuleInvokedWhenContrastBelowThreshold`. |
| `INV-EXP-PALETTE-PERF-01` | Median palette generation latency < 0.1 ms (100 µs) | Benchmark measured 6.58 µs per palette across 10,000 iterations (15x faster than required budget). |

---

## 3. Concrete Implementation Files

1. **`src/Ornare.Core/FolderPalette.cs`:**
   - Immutable record capturing the 5 derived tones, base color, algorithm version, badge foreground, and backing disk indicator.
2. **`src/Ornare.Imaging/OklabColor.cs`:**
   - Lightweight `readonly record struct` for OKLab `(L, a, b)` coordinates.
3. **`src/Ornare.Imaging/OklchColor.cs`:**
   - Lightweight `readonly record struct` for cylindrical OKLCH `(L, C, h)` coordinates.
4. **`src/Ornare.Imaging/ColorConversion.cs`:**
   - Ottosson's published 3x3 matrices in `double` precision.
   - sRGB gamma transfer functions (`SrgbToLinear`, `LinearToSrgb`).
   - 24-iteration binary search gamut mapping (`MapLchToRgb`).
   - Hex parsing and formatting helpers.
5. **`src/Ornare.Imaging/WcagContrast.cs`:**
   - Relative luminance and contrast ratio calculations.
   - Non-text graphical badge foreground contrast evaluation.
6. **`src/Ornare.Imaging/FolderPaletteGenerator.cs`:**
   - Authoritative palette derivation engine integrating tonal shifts, gamut mapping, and badge contrast selection.
7. **`src/Ornare.Imaging/ImagePipeline.cs`:**
   - Integrated `FolderPaletteGenerator` into semantic SVG template recoloring.
8. **`tests/Ornare.Imaging.Tests/AdaptiveContrastTests.cs`:**
   - 16 test cases including known vector round trips, edge cases, 10,000 pseudo-random inputs, WCAG contrast, and performance benchmarks.
9. **`tests/Ornare.IntegrationTests/PerformanceBenchmarkTests.cs`:**
   - Added warm-up iterations adhering to the shared measurement protocol in `00_SHARED_ARCHITECTURE_CONTRACT.md`.

---

## 4. Verification Evidence & Benchmark Results

### 4.1. Unit and Property Tests
- `tests/Ornare.Imaging.Tests/AdaptiveContrastTests.cs`: 16 passed, 0 failed.
- Full `Ornare.Imaging.Tests.dll`: 54 passed, 0 failed.
- Full Solution Test Suite (`Ornare.slnx`): 134 passed, 0 failed, 0 skipped.
  - `Ornare.Core.Tests`: 26 passed
  - `Ornare.Imaging.Tests`: 54 passed
  - `Ornare.IntegrationTests`: 27 passed
  - `Ornare.Storage.Tests`: 18 passed
  - `Ornare.UiTests`: 9 passed

### 4.2. Performance Benchmark
- **Test:** `BenchmarkPaletteGenerationTenThousandIterations` (10,000 iterations after warm-up).
- **Execution Time:** 65.80 ms total.
- **Per-Palette Latency:** 6.58 microseconds.
- **Budget Ceiling:** < 100.0 microseconds (0.1 ms).
- **Margin:** 15.2x faster than requirement.

### 4.3. Platform Compilation Gates
- **Windows 11 x64 Release:** Build succeeded with 0 warnings, 0 errors.
- **Windows 11 ARM64 Release:** All 6 managed projects (`Ornare.Core`, `Ornare.DesignSystem`, `Ornare.Windows`, `Ornare.Storage`, `Ornare.Imaging`, `Ornare.App`) compiled cleanly for `win-arm64` with 0 warnings, 0 errors via `scripts/build-managed-arm64.ps1`.

### 4.4. Real x64 Golden Path E2E
- Ran `scripts/test-real-golden-path.ps1`:
  - Target folder customization applied successfully.
  - `desktop.ini` preserved without clobbering unrelated sections or InfoTip.
  - Shell notification dispatched with 0 Explorer restarts.
  - Result: Complete success.

---

## 5. Conclusion

Feature F10 satisfies all functional, mathematical, security, performance, and architectural requirements defined in `feature-expansion/features/F10_adaptive-contrast.md`. The feature is approved to advance to Feature 4 (**F01 — Folder Colors**).
