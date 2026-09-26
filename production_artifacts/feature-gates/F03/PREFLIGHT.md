# Feature Preflight: F03 — Folder Style Templates

**Feature ID:** F03  
**Specification:** `feature-expansion/features/F03_folder-style-templates.md`  
**Execution Order:** 9 of 27  
**Traceability ID:** `ORN-EXP-F03`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  

---

## 1. Live Implementation Points

- **`src/Ornare.Imaging/Templates/`:**
  - Verify `OrnareClassicTemplate.cs` (v1) against exact contract; preserve existing implementation.
  - Create `OrnareSoftTemplate.cs`: corner radius 24, exact soft geometry, semantic roles (`deep`, `base`, `light`, `highlight`, `shadow`).
  - Create `OrnareFlatTemplate.cs`: corner radius 12, exact flat geometry, semantic roles (`deep`, `base`, `light`, `highlight`, `shadow`).
- **`src/Ornare.Imaging/`:**
  - Create `FolderTemplateDescriptor.cs` with lazy loaders and localized resource keys.
  - Create `FolderTemplateCatalog.cs` containing `ornare-classic`, `ornare-soft`, and `ornare-flat`.
  - Create `FolderTemplateValidator.cs` verifying viewBox, 16px safe boundary, semantic roles, and absence of disallowed elements.
  - Update `FolderTemplateRenderer.cs` to accept `templateId` and `templateVersion` across recipe generation, multi-frame ICO rendering, and PNG previews.
- **`src/Ornare.App/`:**
  - Add template selector in `QuickPanelWindow.xaml` and `ManagerWindow.xaml`.
  - Wire dynamic template switching and debounced preview.
  - Add bilingual English (`en-US`) and Turkish (`tr-TR`) strings.
- **`tests/`:**
  - Create `FolderTemplateTests.cs`:
    - Geometry, role, and security validation for all templates.
    - Multi-color rendering across all 12 curated colors and 10 ICO frame sizes.
    - Visual distinctness between Classic, Soft, and Flat.
    - Immutability and historical version resolution.
    - Catalog metadata load performance benchmark (<1 ms).

---

## 2. Invariants & Gates

- `INV-EXP-TEMPL-01`: Exact 3 templates: `ornare-classic`, `ornare-soft`, `ornare-flat` (Version 1).
- `INV-EXP-TEMPL-02`: Shared base geometry: front panel `x=16, y=88, w=224, h=136`, rear `x=24, y=48, w=208, h=120`, tab `x=24, y=40, w=88, h=64`. All within `16..240` safe boundary.
- `INV-EXP-TEMPL-03`: Exact semantic roles: `base`, `light`, `highlight`, `shadow`, `deep` with clipping to front panel.
- `INV-EXP-TEMPL-04`: Immutable template identity `{TemplateId, TemplateVersion}`.
- `INV-EXP-TEMPL-05`: Lazy loading; metadata loads in <1 ms without parsing SVG bodies at startup.
- `INV-EXP-TEMPL-06`: Zero script-generated SVGs, zero proprietary assets.

---

## 3. Supply Chain & Provenance Decisions

- All templates are 100% original Ornare vector geometries authored specifically for this project.
- No external dependencies; standard .NET and Magick.NET rasterization.
