# F06 — Contain / Cover / Fill

**Status:** IMPLEMENT  
**Dependencies:** F18  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Let the image-placement recipe choose how a source image occupies the icon canvas while keeping behavior deterministic and non-destructive.

## Fixed decisions

1. `Contain` remains default and preserves the full image with transparent/background letterboxing.
2. `Cover` preserves aspect ratio and center-crops to fill the canvas.
3. `Fill` means stretch to the canvas and is deliberately labeled as distortion-capable; because Ornare historically forbids silent distortion, it may only run after explicit user selection.
4. Placement mode is recipe metadata and part of render cache key.

## Integration points

- `Ornare.Imaging`: `ImagePlacementMode` and geometry calculator.
- `Ornare.App`: three-option segmented/radio control in advanced image editor, not primary Quick Panel.
- `Ornare.Core`: recipe serialization.

## Implementation sequence

1. Extract current contain geometry into a pure `ComputePlacement(sourceSize, canvasSize, mode, padding)` function.
2. Implement Cover with scale=max(canvas/source ratios) and centered crop; implement Fill with independent X/Y scale only after explicit mode selection.
3. Render each ICO frame from normalized source using that frame’s placement geometry; never resize a previously generated frame.
4. Persist selected mode in the recipe/history so re-export and repeat apply are deterministic.
5. Add visual preview using the same geometry function at bounded preview resolution.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| Silent image distortion | Contain default; Fill only by explicit user choice and clear iconography/copy. |
| Crop surprises | Cover preview must show exact crop; no face detection magic. |
| Different preview/final result | Share the same geometry calculator. |
| Cache collision | Mode is included in render key. |

## Required tests

- Landscape/portrait/square inputs across all three modes.
- Transparent images and 1px/very large dimensions.
- Preview crop equals final 256px frame geometry.
- Aspect ratio invariants for Contain/Cover; Fill expected distortion.
- Cache key differs by mode.

## Performance requirements

- Geometry calculation allocation-free or near-zero.
- No extra source decode per frame; decode/normalize once, render frames from normalized source.

## Approved sources / research

- **Magick.NET:** https://github.com/dlemstra/Magick.NET

Before copying any source, the agent must re-check the exact upstream commit/license and record provenance according to `../01_OSS_SOURCES_AND_LICENSE_POLICY.md`.

## Forbidden implementations

- No automatic face/content crop.
- No mode-specific duplicate encoder.
- Do not change default from Contain.

## Feature Definition of Done

- Pure geometry tests green.
- Golden render tests green.
- UI tested in TR/EN and keyboard.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F06/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
