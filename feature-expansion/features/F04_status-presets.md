# F04 — Status Presets

**Status:** IMPLEMENT  
**Dependencies:** F03,F01,F10,F18  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Offer a small set of semantic work-status recipes that combine a template, curated color and vector badge without introducing a separate renderer, storage format or folder-mutation path.

## Fixed decisions

1. Initial statuses and visual inputs are exact:
   - `status.in-progress` — base `#2563EB`, Fluent logical glyph `ic_fluent_clock_24_regular`
   - `status.done` — base `#16A34A`, Fluent logical glyph `ic_fluent_checkmark_circle_24_regular`
   - `status.archive` — base `#64748B`, Fluent logical glyph `ic_fluent_archive_24_regular`
   - `status.important` — base `#DC3545`, Fluent logical glyph `ic_fluent_important_24_regular`
2. Use those four monochrome MIT Fluent UI System Icons pinned to an exact upstream commit. If upstream has renamed/removed one logical asset at implementation time, **do not silently substitute a different symbol**: record the upstream change as an external source mismatch, author an Ornare-owned geometrically equivalent symbol for that one semantic meaning, and record that provenance; never use emoji.
3. Presets are immutable versioned recipes: `presetId + presetVersion + templateId/version + palette + badgeAssetId/version + badge layout`; there is no preset-specific database schema.
4. Presets live in Manager/Editor; Quick Panel remains the golden-path surface and does not gain a status grid.
5. Badge compositor geometry is fixed for V1: normalized badge box is `0.34 × 0.34` of the square render canvas, its center is `(0.78, 0.78)`, and minimum outer safe margin is `0.05`; the badge preserves its own aspect ratio inside this box.
6. Badge is **omitted from 16, 20 and 24 px frames** and rendered for 32–256 px frames. This is a deliberate legibility rule, not a claim of F11 optical-size rendering.
7. Badge foreground/backing contrast uses F10. If direct black/white badge foreground cannot satisfy 3:1 against the sampled folder region, render a **solid circular backing disk** centered at `(0.78,0.78)` with diameter `0.40` of canvas; choose black or white backing by whichever has the higher contrast against the sampled folder region, then render the glyph in the opposite black/white color. The glyph remains inside the `0.34×0.34` badge box, so the disk provides a 0.03-canvas ring around it. UI status selection additionally includes a text label so status is never communicated by color alone.

## Integration points

- `Ornare.Core`: versioned `FolderPreset` catalog and stable preset IDs.
- `Ornare.Imaging`: one shared badge compositor and canonical badge recipe fields.
- `Ornare.App`: Manager/Editor selector and localized labels.
- `THIRD-PARTY-NOTICES.md`: exact copied Fluent SVG paths, commit and MIT notice if Fluent assets are used.

## Implementation sequence

1. Define the exact four preset records/colors/glyph logical IDs from Fixed Decision 1 at version `1`; keep visual data in one catalog, not duplicated in XAML.
2. Re-verify Fluent UI System Icons MIT license and the four logical assets at implementation time, pin the exact commit, and copy only those SVGs plus required notice. If a named upstream asset no longer exists, follow Fixed Decision 2 instead of choosing an arbitrary replacement.
3. Normalize/validate each badge through the existing secure SVG policy; no script, external reference, foreign object or network resource.
4. Implement the fixed normalized badge layout and small-frame omission rule exactly.
5. Include all badge/preset output-affecting fields in F18 `RenderKey`.
6. Apply/export/history through existing render/cache/mutation pipelines only.
7. Add accessible localized status labels in UI and golden fixtures for all frame sizes.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| Third-party provenance | Pin exact commit/file/license and record notice; if uncertain, use Ornare-authored shapes instead. |
| Badge unreadable at small sizes | Omit badge at 16/20/24 px; render only at ≥32 px and test golden frames. |
| Color-only meaning | Text label in UI plus distinct badge shape; F10 contrast for rendered mark. |
| Cache collision after visual change | Preset/badge version is part of F18 recipe identity. |
| Quick Panel clutter | Status presets remain in Manager/Editor only. |

## Required tests

- Four preset golden renders at 16/20/24/32/40/48/64/96/128/256 px; badge absent exactly at 16/20/24.
- Preset apply/undo/reset/reboot/source-delete persistence using existing mutation engine.
- Render-key changes when badge asset/version/layout or preset version changes.
- Third-party notice/provenance audit if Fluent assets are copied.
- High Contrast UI selector, keyboard and Narrator labels; no color-only state.

## Performance requirements

- Badge parsing/composition occurs only on render/cache miss.
- No new startup repository, service, watcher or Explorer work.
- Cached preset apply bypasses badge rendering.

## Approved sources / research

- **Fluent UI System Icons:** https://github.com/microsoft/fluentui-system-icons

Before copying source/assets, re-check the exact upstream commit/license and record provenance according to `../01_OSS_SOURCES_AND_LICENSE_POLICY.md`.

## Forbidden implementations

- No emoji badges.
- No raster sticker pack.
- No preset-specific mutation or database engine.
- Do not claim F11 optical-size rendering.

## Feature Definition of Done

- Preset catalog/version/layout rules are fixed and tested.
- License attribution is present if applicable.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F04/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
