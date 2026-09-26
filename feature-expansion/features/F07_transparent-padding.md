# F07 — Transparent Padding

**Status:** IMPLEMENT  
**Dependencies:** F06  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Allow logos/marks to receive controlled inset padding before placement so small icons remain legible and do not touch canvas edges.

## Fixed decisions

1. Advanced Editor exposes a `Padding` toggle that defaults **Off**. Off serializes canonical `PaddingPercent = 0`. Turning it On initializes the control to **8% per side**; accepted values are integer percentages **0–30%**. A user-selected 0% is canonicalized identically to Off for render/cache identity.
2. Padding applies before Contain/Cover/Fill geometry; Cover with padding fills the inner padded rectangle, not the full canvas.
3. Padding value is part of render recipe/cache key.

## Integration points

- `Ornare.Imaging`: extend placement geometry with inset rectangle.
- `Ornare.App`: advanced editor slider/stepper with preview.
- `Ornare.Core`: validated percentage value object.

## Implementation sequence

1. Add `PaddingPercent` validated to [0,30].
2. Compute inner canvas bounds using frame size × percentage; round consistently using midpoint policy documented in tests.
3. Feed inner bounds to F06 placement calculator.
4. Expose a restrained slider plus numeric percentage; changes preview only until commit.
5. Persist value in recipe/history and include in render key.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| Tiny inner canvas | Hard cap at 30%; reject invalid persisted values during migration/deserialization. |
| Rounding differences across sizes | Use one integer rounding rule and test all ICO sizes. |
| UI overcomplexity | Keep under advanced editor, not Quick Panel primary surface. |

## Required tests

- 0%, 8%, 30% geometry for all target frame sizes.
- Invalid negative/>30 values rejected.
- Same recipe yields deterministic pixels/cache key.
- Keyboard adjustment works.

## Performance requirements

- Pure math only; no meaningful runtime cost.
- Do not rerender full ICO on slider movement.

## Approved sources / research

- **Magick.NET:** https://github.com/dlemstra/Magick.NET

Before copying any source, the agent must re-check the exact upstream commit/license and record provenance according to `../01_OSS_SOURCES_AND_LICENSE_POLICY.md`.

## Forbidden implementations

- No auto-padding heuristic in this feature.
- No per-size user slider values.
- No new image library.

## Feature Definition of Done

- Geometry and visual tests green.
- No performance regression.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F07/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
