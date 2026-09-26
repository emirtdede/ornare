# F08 — Background Color

**Status:** IMPLEMENT  
**Dependencies:** F06,F07,F10  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Allow a transparent source/logo to be composited over a chosen solid background without changing the original asset.

## Fixed decisions

1. Background modes: Transparent (default) or SolidColor; no gradients/patterns in this feature.
2. Solid background color **must reuse the F02 WinUI ColorPicker component** and remains separate recipe state from Folder Colors; alpha is not exposed here, so background color is opaque `#RRGGBB`.
3. Decode/profile-normalize the foreground through Ornare's existing secure image pipeline to sRGB while preserving alpha, create an sRGB background canvas, and composite with the project's existing Magick.NET alpha/composite settings before final frame encoding. Do not introduce a second linear-light rendering pipeline unless a separate ADR and golden-image tests prove it is required.
4. Original imported asset bytes are immutable.

## Integration points

- `Ornare.Imaging`: background layer in render recipe/compositor.
- `Ornare.App`: advanced editor control.
- `Ornare.Core`: `BackgroundSpec` discriminated model/enum + color.

## Implementation sequence

1. Add `BackgroundMode` and opaque ARGB/RGB value; transparent mode ignores stored color during canonical serialization.
2. Create the destination canvas with transparent or solid background before placing the normalized source.
3. Ensure alpha-premultiplication/flattening is handled consistently by Magick.NET and ICO/PNG encoders.
4. Reuse ColorPicker component from F02 for solid color selection; preview only until commit.
5. Include normalized background spec in render key and history.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| Halo/fringing around transparent edges | Keep alpha through normalization/composition and test anti-aliased logos. |
| Unexpected mutation of source | Source assets immutable; background exists only in recipe/derived output. |
| Duplicate color UI | Reuse the existing picker component, not a second implementation. |

## Required tests

- Transparent, white, black, saturated backgrounds with semi-transparent PNG/SVG.
- Exported ICO and PNG preserve expected pixels/alpha.
- Cache keys differ only when effective background changes.
- Original asset hash unchanged.

## Performance requirements

- Single composite layer; negligible overhead versus render.
- Preview stays bounded.

## Approved sources / research

- **Magick.NET:** https://github.com/dlemstra/Magick.NET
- **WinUI ColorPicker:** https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/color-picker

Before copying any source, the agent must re-check the exact upstream commit/license and record provenance according to `../01_OSS_SOURCES_AND_LICENSE_POLICY.md`.

## Forbidden implementations

- No gradients/patterns.
- No destructive edit of canonical source.
- No second picker implementation.

## Feature Definition of Done

- Alpha/golden tests green.
- Exports verified.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F08/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
