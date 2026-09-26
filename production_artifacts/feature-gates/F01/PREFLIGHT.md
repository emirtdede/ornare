# Feature Preflight: F01 — Folder Colors

**Feature ID:** F01  
**Specification:** `feature-expansion/features/F01_folder-colors.md`  
**Execution Order:** 4 of 27  
**Traceability ID:** `ORN-EXP-F01`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  

---

## 1. Live Implementation Points

- **`src/Ornare.Core/BuiltInColors.cs`:**
  - Definitive catalog of 12 curated stable color entries (`color.amber` through `color.slate`) plus `Custom`.
  - Immutable record `FolderColorEntry(string Id, string HexValue, string ResourceKey)`.
  - Duplicate-ID and duplicate-hex validation at startup and unit tests.
- **`src/Ornare.Imaging/Templates/OrnareClassicTemplate.cs`:**
  - Original Ornare-authored semantic vector template: `ornare-classic` Version 1.
  - Fixed 256×256 geometry:
    - rear body: `x=24, y=48, w=208, h=120, r=16` (`data-ornare-role="deep"`)
    - tab: `x=24, y=40, w=88, h=64, r=16` (`data-ornare-role="deep"`)
    - front panel: `x=16, y=88, w=224, h=136, r=16` (`data-ornare-role="base"`)
    - clipped role strips on front panel:
      - `light`: `y=88..130` (`data-ornare-role="light"`)
      - `highlight`: `y=88..94` (`data-ornare-role="highlight"`)
      - `shadow`: `y=206..224` (`data-ornare-role="shadow"`)
  - No Apple or Microsoft proprietary SVG/artwork.
- **`src/Ornare.Imaging/FolderTemplateRenderer.cs`:**
  - Bridges `FolderPaletteGenerator` (F10) + `SvgRecolorService` (F09) + `RenderRecipe` / Cache (F18) to produce high-res 10-frame ICOs.
- **`src/Ornare.App/Strings/{en-US, tr-TR}/Resources.resw`:**
  - Localized display names for all 12 curated colors.
- **`src/Ornare.App/`:**
  - Quick Panel compact color row / picker integration.
- **`tests/`:**
  - `FolderColorsTests`: Catalog integrity, template geometry, 12 color ICO renders, render cache hit validation.
  - Integration tests: End-to-end apply, desktop.ini preservation, rename/move, and undo/reset.

---

## 2. Invariants & Gates

- `INV-EXP-COLOR-01`: Exact 12 curated colors from specification preserved without drift.
- `INV-EXP-TEMPLATE-01`: Author original `ornare-classic` semantic template with documented provenance.
- `INV-EXP-CACHE-01`: Repeated color applications hit F18 render cache with zero decode/encode.
- `INV-EXP-APPLY-01`: Consumes identical `FolderCustomizer` transaction, rollback, and relative icon path (`.ornare\folder.ico`).
- `INV-EXP-PERF-01`: Render-cache lookup/copy stage P95 < 10 ms.

---

## 3. Supply Chain & Provenance Decisions

- Template is authored 100% originally in vector math; no external SVG files copied or traced.
- Provenance recorded in `THIRD-PARTY-NOTICES.md`.

---

## 4. Preflight Baseline

- All 134 solution tests currently green.
- x64 and ARM64 Release builds green with 0 errors, 0 warnings.
- F18, F09, F10 gates green.
