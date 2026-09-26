# F03 — Folder Style Templates

**Status:** IMPLEMENT  
**Dependencies:** F01,F09,F10,F18  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Ship exactly three original Ornare folder silhouettes/styles that consume the same semantic palette and renderer, with enough geometric constraints that they remain a coherent family rather than agent-chosen unrelated designs.

## Fixed decisions

1. Initial catalog contains exactly:
   - `ornare-classic` version 1
   - `ornare-soft` version 1
   - `ornare-flat` version 1
2. Every built-in template uses `viewBox="0 0 256 256"`, contains no text/font/embedded raster/script/external reference/`foreignObject`, and keeps visible geometry inside a 16 px safe boundary (`x/y` 16–240).
3. All three use the following exact base geometry on the 256×256 canvas; no agent-selected alternatives are allowed in V1:
   - overall front-panel bounds: `x=16, y=88, width=224, height=136`, bottom edge `y=224`;
   - rear body bounds: `x=24, y=48, width=208, height=120`;
   - tab bounds: `x=24, y=40, width=88, height=64`;
   - all visible geometry remains within `x/y=16..240`;
   - front-panel width is therefore exactly 87.5% of the canvas and tab width is exactly 39.3% of the front-panel width.
4. Exact V1 style geometry/role contract:
   - **ornare-classic:** rear/tab/front corner radius `16`; `deep` = rear body, `shadow` = lower 18 px strip inside front panel (`y=206..224`), `base` = full front panel, `light` = top 42 px strip inside front panel (`y=88..130`), `highlight` = 6 px strip at `y=88..94`; no blur/filter.
   - **ornare-soft:** rear/tab/front corner radius `24`; `deep` = rear body, `shadow` = lower 12 px strip (`y=212..224`), `base` = full front panel, `light` = top 52 px strip (`y=88..140`), `highlight` = 8 px strip at `y=88..96`; no blur/filter.
   - **ornare-flat:** rear/tab/front corner radius `12`; `deep` = rear body, `shadow` = lower 8 px strip (`y=216..224`), `base` = full front panel, `light` = top 32 px strip (`y=88..120`), `highlight` = 4 px strip at `y=88..92`; no gradient/filter/opacity shadow effect.
   - Role strips must be clipped to the same front-panel rounded-rectangle geometry; they are semantic color planes, not independent rectangles protruding beyond the silhouette.
5. Each template must include exactly the semantic roles `base`, `light`, `highlight`, `shadow`, `deep` using F09 `data-ornare-role`; V1 role geometry is exactly the geometry assigned in Fixed Decision 4 and no additional paint role is allowed.
6. Template identity `{TemplateId, TemplateVersion}` is immutable after release. Any pixel/geometry/role change increments `TemplateVersion`; historical render compatibility for referenced old versions is retained until an explicit migration retires it.
7. Catalog metadata loads without parsing SVG bodies; SVG content is lazy-loaded only for preview/render.
8. The uploaded `Windows_11_FOLDER.svg` is **not** a production template unless provenance/license is independently cleared. Even if cleared, these three Ornare templates remain independently authored and must not be traced copies.

## Integration points

- `Ornare.Imaging`: `FolderTemplateCatalog`, validator, lazy template loader.
- `Ornare.App`: Manager/Editor three-option template selector with on-demand preview.
- `Ornare.Storage`: recipe stores ID/version only; no SVG duplication per asset.

## Implementation sequence

1. **Do not re-author or mutate `ornare-classic` v1 created by F01.** Verify it byte/semantic-equivalently against the fixed Classic contract, then manually author only `ornare-soft` v1 and `ornare-flat` v1 according to the fixed geometry/style rules; production SVG source must not be script-generated.
2. Add a test/build validator (audit tooling is allowed) that verifies:
   - exact viewBox;
   - safe boundary;
   - required semantic roles;
   - no disallowed SVG content;
   - catalog ID/version uniqueness;
   - renderability through the existing Magick.NET path.
3. Implement catalog metadata with stable localized name resource keys; do not parse SVG at startup.
4. Render preview only on demand and cache by template ID/version + F18 canonical recipe.
5. Persist template ID/version in recipe/history.
6. Preserve renderer access to an old referenced version after a newer version is added; UI catalog may hide old version for new selections but history/export/apply-last must remain reproducible.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| Visual family drifts | Fixed shared canvas/proportions/role contract and golden review. |
| Template licensing | Only original Ornare or explicitly compatible proven assets ship. |
| Historical recipe drift | Immutable versioned identity. |
| Startup cost | Metadata only at startup; SVG parse/render on demand. |
| Semantic mismatch | Automated validator rejects missing/unknown-required roles and disallowed content. |

## Required tests

- All three templates pass exact geometry/security/semantic validator.
- Render all F01 curated colors and all qualified ICO frames.
- Golden image set proves Classic/Soft/Flat are visually distinct but share the same folder family proportions.
- Add v2 fixture without changing v1 output: historical v1 recipe remains byte/pixel reproducible within encoder determinism contract.
- Selector keyboard/UIA/Light/Dark/High Contrast localization checks.
- Startup trace proves catalog does not parse all SVG files.

## Performance requirements

- Catalog metadata load target `<1 ms typical` on benchmark machine.
- SVG parse/render only on first requested template/recipe; cache prevents repeat work.
- No additional shell/Explorer work.

## Approved sources / research

- **Magick.NET:** https://github.com/dlemstra/Magick.NET

## Forbidden implementations

- No extra template pack in this feature.
- No runtime network template download.
- No script-generated production SVG.
- No copied/traced Microsoft/Folder11/restricted geometry.
- No unversioned visual edits to a shipped template ID/version.

## Feature Definition of Done

- Three manually authored templates reviewed against fixed geometry rules.
- Validator/versioning/history reproduction tests green.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F03/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
