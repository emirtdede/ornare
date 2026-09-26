# F18 — Asset Deduplication

**Status:** IMPLEMENT  
**Dependencies:** none; foundation  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Guarantee that canonical source assets and deterministic derived renders are stored once per identity, while making render-cache identity complete enough that no two visually different recipes can collide.

## Fixed decisions

1. `AssetHash = SHA-256(canonical imported source bytes)`; no perceptual/fuzzy hash participates in identity.
2. `RenderKey` is a separate SHA-256 over a **canonical versioned render recipe**; filesystem path is never identity.
3. The canonical recipe must include **every output-affecting field**. At minimum:
   - `RecipeVersion`;
   - source `AssetHash` or built-in template source identity;
   - source normalization/orientation/frame policy version;
   - renderer/encoder version;
   - template ID + template version;
   - palette algorithm version + exact base/palette parameters;
   - SVG recolor mode/value/version;
   - placement mode;
   - padding value;
   - background mode/color;
   - preset ID/version when applicable;
   - badge asset ID/version + anchor + normalized scale/layout version;
   - target format;
   - exact target frame/size set;
   - resampling/interpolation policy version;
   - alpha/compositing policy version;
   - color-space/profile policy version.
4. Future features that alter output must add their field/version to the canonical recipe or increment the relevant policy/`RecipeVersion` before shipping.
5. Canonical serialization uses fixed field order, explicit type/length boundaries and invariant byte representation; it is unit-tested with golden byte fixtures. Generated/recolored SVG serialization is not the identity.
6. Unique database constraints are final authority under concurrency.
7. `LastAppliedRenderKey`/equivalent committed-last pointer introduced by F21 must reference an immutable retained recipe record. Cleanup cannot delete that recipe/source while the pointer or any assignment/history/reference still requires it.

## Integration points

- `Ornare.Core`: immutable render recipe + canonical serializer.
- `Ornare.Storage`: unique asset/render rows, immutable recipe metadata, reference-safe cleanup.
- `Ornare.Imaging`: pre-render render-key calculation and verified cache.
- `Ornare.App`: no required visual change.

## Implementation sequence

1. Audit existing CAS/schema/path conventions and preserve compatible on-disk identities.
2. Ensure unique source asset hash and unique derived `RenderKey` constraints.
3. Implement/centralize canonical recipe serialization with versioning and golden byte fixtures.
4. Stream SHA-256 for imported source bytes; do not read arbitrary large files wholly into memory just to hash.
5. Compute render identity before expensive render whenever all canonical inputs are known.
6. Cache hit is valid only if metadata + file existence/size + format/header/frame validation pass; corruption invalidates derived cache and triggers safe rebuild.
7. Cache miss writes to Ornare-owned temp, flushes/validates, then atomically publishes under hash-derived path before metadata commit.
8. Concurrent publish race: one winner; loser reopens/reuses winner and removes only its own temp.
9. Reference-safe cleanup considers assignments, undo/history, exports-in-progress if represented, and F21 last-applied pointer; cleanup is after commit only.
10. Existing operation-journal/startup reconciliation handles crash windows; no down-migration is required.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| RenderKey omits a visual parameter | Exhaustive canonical field list + feature-review rule + golden serializer tests. |
| Concurrent duplicate import/render | UNIQUE constraints + atomic publish + race handling. |
| Corrupt cache accepted | Revalidate cache artifact metadata/format/frames before use. |
| Recipe drift | Version every renderer/template/palette/policy change. |
| Cleanup breaks Apply Last/history | Immutable reference graph; referenced recipe/source cannot be collected. |
| Migration failure | Forward migration transaction rolls back; startup reconciliation handles filesystem temp state. |

## Required tests

- Same PNG imported 100 times concurrently -> one canonical row/file.
- Same render recipe concurrently -> one derived artifact and byte-identical cache result.
- Change each output-affecting recipe field one at a time -> `RenderKey` changes.
- Golden canonical serialization bytes remain stable.
- Delete/corrupt cache -> safe rebuild.
- Crash at temp/publish/DB windows -> reconciliation restores invariant.
- Last-applied/history reference prevents cleanup; removing final reference makes item eligible only under existing cleanup rules.

## Performance requirements

- Cache lookup is cheaper than decode/render.
- Streaming SHA-256 uses bounded memory.
- Performance comparison is **render-stage cache miss vs render-stage cache hit**, excluding folder mutation/journal/shell-refresh time. A valid cache hit must perform zero source decode/raster/ICO encode and have median render-cache resolution time **≤5 ms** on the feature-gate machine. When the corresponding miss median is **≥25 ms**, hit median must also be at least **5× faster**; when miss median is <25 ms, the absolute ≤5 ms hit target is the blocking criterion and the ratio is reported but not used as a failure condition.
- No cache/DB access from Explorer and no Quick Panel startup initialization regression.

## Approved sources / research

- **Magick.NET:** https://github.com/dlemstra/Magick.NET

## Forbidden implementations

- No perceptual hash for canonical deduplication.
- No filename/path identity.
- No generated-SVG-byte identity.
- No cleanup that ignores history/last-applied references.
- No Explorer database query.

## Feature Definition of Done

- Forward migration failure rollback/recovery tested.
- Concurrent dedup and canonical-recipe field coverage tests green.
- Full suite and x64/ARM64 Native AOT green.
- Render-stage cache hit/miss evidence recorded.
- A feature evidence report exists under `production_artifacts/feature-gates/F18/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
