# Feature Preflight: F05 — Priority Presets

**Feature ID:** F05  
**Specification:** `feature-expansion/features/F05_priority-presets.md`  
**Execution Order:** 11 of 27  
**Traceability ID:** `ORN-EXP-F05`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  

---

## 1. Live Implementation Points

- **`src/Ornare.Imaging/Badges/`:**
  - Update `StatusBadgeAssets.cs` to add the three 100% Ornare-authored geometric priority badges (version 1):
    - `ornare_priority_high_24`: filled upward chevron (`M4 16L8 16L12 10L16 16L20 16L12 4Z`)
    - `ornare_priority_medium_24`: filled rounded rectangle (`rect x="4" y="10" width="16" height="4" rx="2" ry="2"`)
    - `ornare_priority_low_24`: filled downward chevron (`M4 8L8 8L12 14L16 8L20 8L12 20Z`)
  - Update `StatusBadgeCompositor.cs` to support both path and polygon/rect shapes or uniform path representation.
- **`src/Ornare.Core/Presets/`:**
  - Update `StatusPresetCatalog.cs`:
    - Add `priority.high`: `#DC3545`, badge `ornare_priority_high_24`, version 1
    - Add `priority.medium`: `#E8A317`, badge `ornare_priority_medium_24`, version 1
    - Add `priority.low`: `#2563EB`, badge `ornare_priority_low_24`, version 1
    - Expose `PriorityPresets` and include in `All`.
- **`src/Ornare.App/`:**
  - Update `ManagerWindow.xaml` and `ManagerWindow.xaml.cs`:
    - Categorize presets into "Workflow Status" and "Visual Priority" sections with clear UI disclaimer that priority is visual organization only.
    - Wire template switching and folder application through the existing F04 pipeline.
  - In `Strings/{en-US, tr-TR}/Resources.resw`:
    - Add localized names, descriptions, section headers, and disclaimer text.
- **`tests/`:**
  - Create `PriorityPresetTests.cs`:
    - Validate exact 3 priority presets (High, Medium, Low) and their base colors.
    - Validate exact geometric badge definitions.
    - Assert small-frame omission (omitted on 16/20/24 px frames; rendered on 32..256 px).
    - Assert deterministic render keys and divergence across presets.
    - Confirm database schema version is unchanged.
    - Benchmark catalog lookups (<1 ms).

---

## 2. Invariants & Gates

- `INV-EXP-PRIO-01`: Exactly three priority presets: `priority.high` (#DC3545), `priority.medium` (#E8A317), `priority.low` (#2563EB) at Version 1.
- `INV-EXP-PRIO-02`: Ornare-authored monochrome geometric SVGs (upward chevron, rounded rectangle, downward chevron), zero external dependencies.
- `INV-EXP-PRIO-03`: Same badge compositor, geometry ($0.34 \times 0.34$ at $0.78, 0.78$), backing disk ($0.40$), and omission rules (16, 20, 24 px omitted) as F04.
- `INV-EXP-PRIO-04`: Priority is presentation metadata only; never alters filesystem attributes or shell sort order.
- `INV-EXP-PRIO-05`: Zero database tables, zero schema migrations; relies entirely on canonical render recipe / history.
- `INV-EXP-PRIO-06`: Bilingual UI includes explicit notice that priority is visual organization only.

---

## 3. Supply Chain & Provenance Decisions

- 100% original Ornare vector geometries authored in code. Zero external assets, zero third-party notices required for F05.
