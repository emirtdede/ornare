# F05 — Priority Presets

**Status:** IMPLEMENT  
**Dependencies:** F04  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Provide High/Medium/Low visual-priority recipes through the same F04 preset/badge/render engine, with no new schema or priority-specific renderer.

## Fixed decisions

1. Presets are exactly:
   - `priority.high` — base `#DC3545`
   - `priority.medium` — base `#E8A317`
   - `priority.low` — base `#2563EB`
2. Priority badges are **Ornare-authored geometric SVGs**, not emoji/text and not a new external dependency. V1 geometry is exact and uses `viewBox="0 0 24 24"`, monochrome `currentColor`, no stroke, filters, gradients or text:
   - High: filled upward chevron polygon points `4,16 8,16 12,10 16,16 20,16 12,4`.
   - Medium: filled rounded rectangle `x=4 y=10 width=16 height=4 rx=2 ry=2`.
   - Low: filled downward chevron polygon points `4,8 8,8 12,14 16,8 20,8 12,20`.
   These exact geometries are version `1`; any coordinate/shape change requires a badge asset version increment.
3. Badge layout/small-frame omission/contrast rules are inherited exactly from F04.
4. Priority is presentation metadata only; it never sets Windows filesystem priority, file attributes, sorting metadata or shell metadata.
5. F05 introduces **no database table or migration**. Preset ID/version is represented only by the existing canonical recipe/history fields.

## Integration points

- F04 preset catalog/badge compositor/history/UI component.
- Add only three catalog entries, three tiny manually authored SVG resources, localized labels and tests unless an existing shared-engine defect must be corrected.

## Implementation sequence

1. Manually author the three 24×24 monochrome geometric badge SVGs; no script-generated production SVG.
2. Add the exact three versioned preset definitions and base colors to the shared preset catalog.
3. Validate badge SVGs with F09 secure/static SVG validator.
4. Add localized labels and clear UI copy stating priority is visual organization only.
5. Apply/export/history through the same F04/F18 pipeline.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| Semantic confusion | UI explicitly says priority is visual organization, not filesystem/system priority. |
| Engine/schema duplication | No new renderer/repository/table/migration. |
| Color-only communication | Distinct geometric badge + UI text label. |
| Small-size clutter | Inherit F04 omission at 16/20/24 px exactly. |

## Required tests

- High/Medium/Low exact color/catalog IDs render/apply.
- Badge validator and F04 size/layout rules.
- High Contrast/color-blind UI state does not rely solely on color.
- Database schema/version is unchanged by F05.
- Render key changes by preset ID/version/badge asset version as F18 requires.

## Performance requirements

- Data/resource-only addition on F04 engine; no new startup service/repository and no measurable Explorer work.
- Badge render only on cache miss.

## Approved sources / research

No third-party badge asset is required for F05; use existing Ornare SVG tooling and F04 compositor.

## Forbidden implementations

- No separate priority engine.
- No emoji/text baked into folder icon.
- No database migration/table.
- No script-generated production SVG.

## Feature Definition of Done

- Three exact versioned presets and manually authored badges pass validator/golden tests.
- Schema unchanged.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F05/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
