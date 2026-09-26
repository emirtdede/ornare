# F11 — Per-Size Optical Rendering

**Status:** DEFERRED BY PRODUCT DECISION  
**Dependencies:** future explicit owner reactivation  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Reserve a correct future path for intentionally different small/medium/large icon artwork without pretending that independent resizing is optical redesign.

## Fixed decisions

1. **No production implementation is added in this expansion.**
2. Existing independent rendering of every ICO frame from the normalized source remains valid and must not be labeled `Per-Size Optical Rendering`.
3. No mature drop-in open-source engine was identified that can safely generate Ornare-quality optical variants automatically; restricted/reference assets are not copied.
4. If the owner later reactivates F11, V1 of the feature uses manually authored semantic SVG variants, not AI/vector auto-simplification.
5. Future fixed size bands are already decided: `Small = 16,20`; `Medium = 24,32,40`; `Large = 48,64,96,128,256`.
6. Do not add speculative interfaces/classes/tables today solely for deferred F11.

## Integration points

None in the current expansion. Future integration, after explicit owner reactivation, is limited to the template/variant resolver in `Ornare.Imaging` and built-in template catalog.

## Implementation sequence

1. Current expansion: make no production code/schema/UI change for F11.
2. Preserve this decision document and research references.
3. Create `production_artifacts/feature-gates/F11/REPORT.md` stating exactly `DEFERRED BY PRODUCT DECISION`, confirming no F11 production code was added and documenting the fixed future size bands.
4. When explicitly reactivated in a future scope, require manually authored Small/Medium/Large SVGs per built-in template and Explorer screenshot/golden review for every qualified ICO frame.
5. Automatic simplification, if ever desired, is a separate future feature/ADR after manual variants are proven.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| False completion via resize | Explicitly forbidden; current per-frame resize/render is not F11. |
| Premature architecture | No speculative code/schema/API in this expansion. |
| Restricted asset reuse | Folder11/restricted assets are reference-only unless a future legal review explicitly permits commercial reuse. |
| Future performance cost | Manual variants are selected before rasterization and preserve current single-frame-at-a-time render/cache model. |

## Required tests

Current F11-specific test requirement is only:
- verify the pre-existing ICO pipeline still emits all qualified frames and no change in this expansion is mislabeled F11.

The **global expansion gate** still runs the normal full x64/ARM64 build/test/package regressions; F11 itself does not require a dedicated new build, performance delta or implementation test because it is intentionally deferred.

## Performance requirements

No F11 production code is added; therefore F11 introduces zero runtime/startup/package-size cost.

## Approved sources / research

- **Folder11 repository (reference only; obey current license):** https://github.com/icon11-community/Folder11
- **Folder11 optical/small-size issue reference:** https://github.com/icon11-community/Folder11/issues/193

## Forbidden implementations

- No generic resize marketed as optical rendering.
- No automatic AI/vector simplifier.
- No restricted asset copying.
- No speculative variant framework solely to make the feature appear implemented.

## Feature Definition of Done

- `production_artifacts/feature-gates/F11/REPORT.md` exists with `DEFERRED BY PRODUCT DECISION`, fixed future size bands, research links and confirmation that no production implementation was added.
- Existing qualified ICO frame regression remains green.
- F11 is excluded from “implemented feature count” until explicitly reactivated.
