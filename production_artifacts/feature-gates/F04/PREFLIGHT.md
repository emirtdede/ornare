# Feature Preflight: F04 — Status Presets

**Feature ID:** F04  
**Specification:** `feature-expansion/features/F04_status-presets.md`  
**Execution Order:** 10 of 27  
**Traceability ID:** `ORN-EXP-F04`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  

---

## 1. Live Implementation Points

- **`src/Ornare.Core/Presets/`:**
  - Create `StatusPreset.cs` immutable record: `{ Id, Version, BaseHex, BadgeAssetId, BadgeVersion, DisplayNameResourceKey, DescriptionResourceKey }`.
  - Create `StatusPresetCatalog.cs`:
    - `status.in-progress`: `#2563EB`, `ic_fluent_clock_24_regular`
    - `status.done`: `#16A34A`, `ic_fluent_checkmark_circle_24_regular`
    - `status.archive`: `#64748B`, `ic_fluent_archive_24_regular`
    - `status.important`: `#DC3545`, `ic_fluent_important_24_regular`
- **`src/Ornare.Imaging/Badges/`:**
  - Create `StatusBadgeAssets.cs`:
    - Clean monochrome vector definitions from Microsoft Fluent UI System Icons (pinned commit `a563cf9166f4f91aa617557ed272612b7f0a2f72`).
  - Create `StatusBadgeCompositor.cs`:
    - Canvas geometry: badge box $0.34 \times 0.34$, center $(0.78, 0.78)$, minimum outer margin $0.05$.
    - Circular backing disk: diameter $0.40$ of canvas centered at $(0.78, 0.78)$, contrast-adapted.
    - **Omission rule:** Badge is completely omitted on 16, 20, and 24 px frames; rendered on 32..256 px.
- **`src/Ornare.Imaging/FolderTemplateRenderer.cs`:**
  - Implement `CreatePresetRecipe`, `RenderPresetIco`, and `RenderPresetPreviewPng`.
- **`src/Ornare.App/`:**
  - In `ManagerWindow.xaml`: Add Presets navigation section with live preview, template switcher, and "Apply to Folder..." action.
  - In `Strings/{en-US, tr-TR}/Resources.resw`: Add bilingual preset names, descriptions, and UI labels.
- **`THIRD-PARTY-NOTICES.md`:**
  - Add section 8 recording Fluent UI System Icons MIT license, commit SHA, and exact 4 assets.
- **`tests/`:**
  - Create `StatusPresetTests.cs`:
    - Golden renders across all 10 ICO frame sizes for all 4 presets.
    - Assert badge is absent on 16, 20, 24 px frames and present on 32..256 px frames.
    - Recipe determinism and canonical key divergence when preset/badge/version changes.
    - End-to-end folder customization apply/undo/reset with status preset.
    - Benchmark metadata loading and badge rasterization.

---

## 2. Invariants & Gates

- `INV-EXP-PRESET-01`: Exact 4 initial presets: `status.in-progress`, `status.done`, `status.archive`, `status.important` (Version 1).
- `INV-EXP-PRESET-02`: Monochrome MIT Fluent UI System Icons pinned to commit `a563cf9166f4f91aa617557ed272612b7f0a2f72`.
- `INV-EXP-PRESET-03`: Fixed badge box $0.34 \times 0.34$, center $(0.78, 0.78)$, margin $0.05$.
- `INV-EXP-PRESET-04`: Small-frame omission: badge omitted from 16, 20, and 24 px frames.
- `INV-EXP-PRESET-05`: High-contrast circular backing disk ($0.40$ diameter) ensuring WCAG 3:1+ readability.
- `INV-EXP-PRESET-06`: Preset recipe identity includes `{PresetId, PresetVersion, BadgeAssetId, BadgeVersion}` in F18 render key.
- `INV-EXP-PRESET-07`: Accessible text labels in UI; status is never communicated by color alone.
- `INV-EXP-PRESET-08`: Presets live in Manager/Editor; Quick Panel remains minimal without status grid.

---

## 3. Supply Chain & Provenance Decisions

- Microsoft Fluent UI System Icons (MIT License, Copyright Microsoft Corporation).
- Pinned commit: `a563cf9166f4f91aa617557ed272612b7f0a2f72`.
- Audited SVGs: 4 files copied with exact vector paths and zero external references or scripting.
- Notice recorded in `THIRD-PARTY-NOTICES.md`.
