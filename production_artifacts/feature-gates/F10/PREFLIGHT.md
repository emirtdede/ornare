# Feature Preflight: F10 — Adaptive Contrast / Palette Generation

**Feature ID:** F10  
**Specification:** `feature-expansion/features/F10_adaptive-contrast.md`  
**Execution Order:** 3 of 27  
**Traceability ID:** `ORN-EXP-F10`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  

---

## 1. Live Implementation Points

- **`src/Ornare.Imaging/OklabColor.cs` & `src/Ornare.Imaging/OklchColor.cs`:**
  - Lightweight immutable value types (`readonly record struct`) for OKLab `(L, a, b)` and OKLCH `(L, C, h)`.
- **`src/Ornare.Imaging/ColorConversion.cs`:**
  - sRGB ↔ linear-sRGB (gamma transfer functions).
  - linear-sRGB ↔ OKLab via Björn Ottosson's published 3x3 matrices in `double` precision.
  - OKLab ↔ OKLCH conversion with threshold handling for near-zero chroma (`C < 1e-4`).
  - Monotonic chroma gamut mapping by binary search (max 24 iterations, epsilon `1e-7`) preserving hue and original base color.
- **`src/Ornare.Imaging/WcagContrast.cs`:**
  - WCAG 2.2 relative luminance and contrast ratio calculations.
  - Non-text graphical indicator contrast selection (black or white foreground with ≥ 3:1 ratio, circular backing fallback).
- **`src/Ornare.Imaging/FolderPaletteGenerator.cs`:**
  - Derives `deep` (`L - 0.18`), `shadow` (`L - 0.11`), `base` (exact original), `light` (`L + 0.08`), and `highlight` (`L + 0.14`).
  - Clamps derived tone lightness to `[0.02, 0.98]` without altering base.
- **`src/Ornare.Core/SemanticColorPalette.cs`:**
  - Wire to `FolderPaletteGenerator` so that semantic SVG recoloring and preview rendering utilize the OKLab/OKLCH color system.
- **`tests/Ornare.Imaging.Tests/AdaptiveContrastTests.cs`:**
  - Known vector roundtrip tests.
  - Base-color exact preservation property tests.
  - Edge cases (pure black, pure white, grayscale, near-black, near-white, pure primaries).
  - 10,000 deterministic pseudo-random sRGB inputs proving finite outputs, gamut containment, and monotonic lightness ordering.
  - Badge contrast and backing shape fallback tests.
  - Performance benchmark (< 0.1 ms target).

---

## 2. Invariants & Gates

- `INV-EXP-PALETTE-01`: Base color preserved exactly in 8-bit hex and RGB channels.
- `INV-EXP-PALETTE-02`: Monotonic chroma reduction via binary search (never first-pass clipping).
- `INV-EXP-PALETTE-03`: Derived tone lightness offsets (`-0.18`, `-0.11`, `+0.08`, `+0.14`) clamped to `[0.02, 0.98]`.
- `INV-EXP-PALETTE-04`: Neutral tone generation (`C = 0`) when base chroma < `1e-4`.
- `INV-EXP-PALETTE-05`: WCAG non-text contrast ≥ 3:1 with circular backing fallback.
- `INV-EXP-PALETTE-PERF-01`: Palette generation < 0.1 ms median.

---

## 3. Supply Chain & OSS Decisions

- No third-party color science package added. Pure mathematical implementation using Björn Ottosson's published OKLab specification (`https://bottosson.github.io/posts/oklab/`).
- Matrices and references cited in code headers and notices.

---

## 4. Preflight Baseline

- Full solution tests currently passing: 118 passed, 0 failed.
- x64 Release build: 0 warnings, 0 errors.
- ARM64 Release build: 0 warnings, 0 errors across 6 managed projects.
- F18 & F09 gates green.
