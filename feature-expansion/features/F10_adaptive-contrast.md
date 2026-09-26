# F10 — Adaptive Contrast / Palette Generation

**Status:** IMPLEMENT  
**Dependencies:** F09,F18  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Convert one user-selected opaque sRGB base color into a deterministic five-tone folder palette while preserving the exact selected base color and preserving hue/chroma for derived tones as far as the sRGB gamut allows.

## Fixed decisions

1. Implement a small pure-math OKLab/OKLCH conversion module inside `Ornare.Imaging`; do not add a color-science package.
2. Semantic tones are exactly `deep`, `shadow`, `base`, `light`, `highlight`.
3. The user-selected `base` RGB value is **never altered** for palette storage, UI display, recipe identity, or the base semantic role.
4. Convert the exact base RGB to OKLCH and derive preferred lightness targets from its `L`: `deep = L - 0.18`, `shadow = L - 0.11`, `light = L + 0.08`, `highlight = L + 0.14`.
5. Clamp only **derived** tone lightness to `[0.02, 0.98]`. Do **not** impose a universal minimum spacing between adjacent tones: at near-black/near-white extremes that would contradict preserving the exact base. Tone collapse at a boundary is allowed and must be covered by golden tests.
6. For each derived tone, preserve hue and initial chroma; if out of sRGB gamut, monotonically reduce chroma by binary search until all linear-sRGB channels are within `[0,1]` to epsilon `1e-7`, with at most 24 iterations. Never solve gamut by first clipping RGB channels.
7. If base chroma is `< 1e-4`, treat hue as undefined and generate neutral derived tones (`C = 0`) at the target lightness values.
8. Folder/badge foreground selection uses WCAG **non-text** contrast where it applies: choose black or white by the higher contrast ratio and require at least **3:1** against the sampled background for a required graphical indicator; if the direct glyph result is below 3:1, invoke the exact F04 circular-backing rule (0.40-canvas disk at center 0.78/0.78, higher-contrast black/white disk, opposite-color glyph) rather than shipping an unreadable badge.
9. Canonical recipe serialization stores 8-bit base RGB plus palette algorithm version; it does not serialize platform-dependent floating-point text as the primary identity.

## Integration points

- `Ornare.Imaging`: `OklabColor`, `OklchColor`, conversion helpers, `FolderPaletteGenerator`, `PaletteAlgorithmVersion`.
- `Ornare.Core`: immutable `FolderPalette` and stable palette algorithm version in render recipe.
- `Ornare.App`: preview consumes the palette; no color conversion logic in XAML code-behind.

## Implementation sequence

1. Implement sRGB byte ↔ normalized sRGB ↔ linear-sRGB ↔ OKLab conversions using Björn Ottosson's published OKLab matrices and `double` precision; convert to 8-bit channels only at output boundaries.
2. Implement OKLab ↔ OKLCH with explicit handling for near-zero chroma.
3. Implement the four derived lightness targets exactly as defined above; preserve the exact original base RGB separately.
4. Implement fixed-iteration chroma gamut mapping by binary search; add guards for NaN/Infinity and deterministic termination.
5. Implement WCAG relative-luminance/contrast helper for generated graphical indicators; it is not a replacement for Windows High Contrast UI behavior.
6. Add palette algorithm version to F18's canonical render recipe; changing any palette formula later requires incrementing this version.
7. Add golden render fixtures covering very dark, very light, neutral and saturated inputs before F01 consumes this palette in production.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| Impossible lightness constraints at black/white extremes | Preserve exact base and allow derived boundary tones to converge; do not enforce a contradictory minimum-spacing rule. |
| Hue shift from gamut handling | Reduce OKLCH chroma before RGB conversion; do not use first-pass channel clipping as the gamut strategy. |
| Floating-point instability | Fixed formulas, `double`, fixed 24-iteration maximum, explicit epsilon and palette algorithm version; compare final 8-bit results in cross-architecture tests. |
| Readability of status/priority glyphs | Apply 3:1 non-text contrast rule where the glyph communicates state; if black/white alone cannot satisfy it, use the compositor's contrasting backing shape. |
| Accessibility overclaim | Generated-icon contrast is separate from app UI High Contrast/Narrator/keyboard qualification. |

## Required tests

- Published/independently calculated known vectors for sRGB ↔ OKLab round trips within explicit numeric tolerance.
- Exact base-color preservation for all 256-level channel combinations in a representative property sample.
- Black, white, gray, near-black, near-white and saturated red/green/blue edge cases.
- At least 10,000 deterministic pseudo-random sRGB inputs: all derived outputs finite, in gamut, and ordered `deep.L ≤ shadow.L ≤ base.L ≤ light.L ≤ highlight.L`, allowing equality at clamped boundaries.
- Execute the canonical 8-bit palette corpus on the certified x64 runtime and keep the identical deterministic corpus in the ARM64 test project/build; ARM64 compilation/AOT must remain green, while executing the corpus on physical ARM64 is recorded `NOT PROVEN / PENDING HARDWARE CERTIFICATION` until hardware is available. Do not claim cross-architecture runtime parity without that execution evidence.
- Golden folder/badge renders for representative light/dark/saturated colors.
- Badge contrast test: if a required indicator cannot achieve 3:1 with black/white directly, backing-shape fallback is used.

## Performance requirements

- Palette generation target: median `<0.1 ms` per base color on the feature-gate benchmark machine after warm-up.
- No allocation-heavy LINQ in hot conversion loops; this feature operates on a handful of colors, never per image pixel.
- No work is performed in `explorer.exe`.

## Approved sources / research

- **OKLab reference / matrices:** https://bottosson.github.io/posts/oklab/
- **WCAG 2.2 non-text contrast (3:1):** https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html
- **WCAG relative luminance background:** https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html

The agent must re-check the authoritative text it implements and record any copied formula provenance in `THIRD-PARTY-NOTICES.md`/engineering notes as appropriate; do not copy unrelated source code.

## Forbidden implementations

- No HSL-only naive production palette.
- No universal `0.04` tone-spacing requirement that changes the user's selected base.
- No arbitrary saturation boost.
- No first-pass RGB clipping used as the gamut-mapping algorithm.
- No network/color service.

## Feature Definition of Done

- Conversion/property/gamut/contrast tests green.
- Golden visual set reviewed.
- Palette algorithm version recorded in the canonical recipe.
- Full feature gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F10/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
