# F01 — Folder Colors

**Status:** IMPLEMENT  
**Dependencies:** F09,F10,F18  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Allow a user to choose a color and generate a durable multi-resolution folder icon using Ornare’s semantic vector template and existing apply transaction.

## Fixed decisions

1. The feature uses exactly one render engine: semantic SVG → Magick.NET raster → existing ICO encoder/cache.
2. Initial built-in palette is exactly these stable IDs/base sRGB values, plus `Custom`:
   - `color.amber` `#E8A317`
   - `color.orange` `#F97316`
   - `color.red` `#DC3545`
   - `color.rose` `#E11D48`
   - `color.magenta` `#C026D3`
   - `color.purple` `#7C3AED`
   - `color.indigo` `#4F46E5`
   - `color.blue` `#2563EB`
   - `color.cyan` `#0891B2`
   - `color.teal` `#0F766E`
   - `color.green` `#16A34A`
   - `color.slate` `#64748B`
   Store IDs/hex values once in neutral data; localized display names are resources, never XAML literals.
3. Applying a color is an icon apply operation; it must use the same journal/undo/reset ownership semantics as an image-derived icon.
4. The user-provided `Windows_11_FOLDER.svg` is not shipped until provenance/license is documented; if it is not cleared, the agent must author an original Ornare folder template with equivalent semantic roles rather than trace/copy a proprietary asset.

## Integration points

- `Ornare.Imaging`: folder-template renderer and color render recipe.
- `Ornare.Storage`: store recipe metadata and derived render key, not duplicate full template per color.
- `Ornare.Core`: `ApplyFolderStyle` maps to the same mutation pipeline used by `ApplyFolderIcon` after a durable icon artifact exists.
- `Ornare.App`: compact color entry point; advanced palette in Manager, Quick Panel remains simple.

## Implementation sequence

1. Audit the current ICO generator and expose a render-recipe entry point instead of adding a second encoder.
2. Create the first release-approved semantic template as **`ornare-classic` version 1**; this is the same Classic template later retained by F03, not a temporary/anonymous template. Its 256×256 geometry is fixed: rear body `x=24,y=48,w=208,h=120,r=16`; tab `x=24,y=40,w=88,h=64,r=16`; front panel `x=16,y=88,w=224,h=136,r=16`; semantic role strips are `deep=rear body`, `shadow=y206..224`, `base=front panel`, `light=y88..130`, `highlight=y88..94`, clipped to the front rounded rectangle. Store template ID/version in the recipe.
3. Define the exact 12 IDs/hex values from Fixed Decision 2 in localized-neutral data and add duplicate-ID/value validation; UI labels come from resources keyed by stable ID.
4. On preview, recolor/render only a bounded preview (maximum 256/512 px); do not build a full ICO for every hover.
5. On user commit, create `FolderStyleRecipe`, generate palette (F10), compute render key (F18), reuse cached ICO or render required frames, then call the existing durable folder apply operation.
6. Record history with recipe metadata so Undo/Reset and export remain explainable.
7. After success, show real operation state and existing Undo feedback; no fake delay.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| Unlicensed template asset | Fail release packaging if provenance is absent; ship original Ornare-authored template instead. |
| Performance from preview rerender | Debounce preview changes and render bounded preview only; full ICO only on commit/cache miss. |
| Different colors create duplicate storage | Derived render cache keyed by recipe; reused across folders. |
| Folder mutation divergence | No special color mutation path; consume the same apply transaction. |

## Required tests

- All 12 exact curated IDs/hex values render valid ICOs with required frames; catalog validation fails on ID/value drift or duplicate IDs.
- Apply a color, delete app source assets, reboot/rename/move within supported NTFS: icon remains.
- Repeated same color/template uses render cache.
- Undo/Reset restore correct baseline without clobbering unrelated desktop.ini.
- Light/Dark/HC/TR/EN visual and accessibility checks for selector.

## Performance requirements

- No Quick Panel startup cost before color UI is opened beyond small static resource metadata.
- Cached color apply must perform **zero image decode and zero ICO encode**; the render-cache lookup/copy stage target is P95 `<10 ms` on the feature-gate benchmark machine, while total folder-apply latency remains governed by the shared apply threshold.
- No SQLite/database access occurs in `explorer.exe`; app-side cache/history database reads and writes are permitted only after Ornare activation and remain off the Explorer callback path.

## Approved sources / research

- **FolderColorizer:** https://github.com/dszharikov/FolderColorizer
- **Magick.NET:** https://github.com/dlemstra/Magick.NET
- **Microsoft folder customization:** https://learn.microsoft.com/en-us/windows/win32/shell/how-to-customize-folders-with-desktop-ini

Before copying any source, the agent must re-check the exact upstream commit/license and record provenance according to `../01_OSS_SOURCES_AND_LICENSE_POLICY.md`.

## Forbidden implementations

- Do not hard-code 12 pre-rendered ICO files as the color system.
- Do not recolor a raster screenshot of a Windows folder.
- Do not bypass journal/history to make color apply faster.

## Feature Definition of Done

- First render + cache hit evidence retained.
- Persistence/undo/reset tests pass.
- Template provenance recorded.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F01/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
