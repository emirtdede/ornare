# Feature Gate Report: F03 — Folder Style Templates

**Feature ID:** F03  
**Specification:** `feature-expansion/features/F03_folder-style-templates.md`  
**Execution Order:** 9 of 27  
**Traceability ID:** `ORN-EXP-F03`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Completion Date:** 2026-09-26  

---

## 1. Executive Summary

Feature F03 introduces a versioned folder style template catalog and multi-silhouette rendering system to Ornare. The catalog includes exactly three release-approved original vector templates: `ornare-classic` (v1, corner radius 16), `ornare-soft` (v1, corner radius 24), and `ornare-flat` (v1, corner radius 12). All three templates adhere strictly to fixed canvas proportions (256x256, 16px safe boundary, front panel width 87.5% of canvas, tab width 39.3% of front panel) and consume the identical 5-tone semantic palette (`base`, `light`, `highlight`, `shadow`, `deep`). Catalog metadata loads in sub-millisecond time (<1 ms) without parsing SVG bodies, while a strict security and geometry validator verifies zero-disallowed content and bounding box conformance. The Quick Panel UI provides interactive template selection with debounced preview.

---

## 2. Invariants & Gates Verification

| Invariant | Description | Verification Evidence | Status |
|---|---|---|---|
| `INV-EXP-TEMPL-01` | Initial catalog contains exactly `ornare-classic`, `ornare-soft`, `ornare-flat` (Version 1) | Tested in `CatalogContainsExactThreeTemplatesWithVersionOne`. | **PASS** |
| `INV-EXP-TEMPL-02` | Exact shared base geometry on 256x256 canvas within 16..240 safe boundary; front panel 87.5% and tab 39.3% | Tested in `TemplatesAdhereToExactProportionRules` and `AllCatalogTemplatesPassValidator`. | **PASS** |
| `INV-EXP-TEMPL-03` | Exact semantic roles: `base`, `light`, `highlight`, `shadow`, `deep` clipped to front panel | Validated by `FolderTemplateValidator` across all catalog templates. Role strips are clipped to front rounded rectangles. | **PASS** |
| `INV-EXP-TEMPL-04` | Template identity `{TemplateId, TemplateVersion}` is immutable after release; recipe determinism diverges | Tested in `RecipeDeterminismIncludesTemplateIdentity`. Recipes with distinct template identities produce distinct canonical keys. | **PASS** |
| `INV-EXP-TEMPL-05` | Lazy loading: metadata loads without parsing SVG bodies at startup; benchmark <1 ms | Tested in `CatalogMetadataLoadsInSubMillisecond`. Average enumeration time is sub-microsecond. | **PASS** |
| `INV-EXP-TEMPL-06` | Zero script-generated production SVGs, zero proprietary assets; 100% original Ornare vector geometries | Manually authored C# template files with clean XML structures. | **PASS** |

---

## 3. Implementation Details

### A. Template Authoring (`src/Ornare.Imaging/Templates/`)
- Preserved `OrnareClassicTemplate.cs` (v1): Corner radius 16, light h=42, highlight h=6, shadow h=18.
- Created `OrnareSoftTemplate.cs` (v1): Corner radius 24, light h=52, highlight h=8, shadow h=12.
- Created `OrnareFlatTemplate.cs` (v1): Corner radius 12, light h=32, highlight h=4, shadow h=8.
- Created `FolderTemplateDescriptor.cs`: Lazy loader wrapper with `DisplayNameResourceKey`, `SvgContent`, and `GetBytes()`.
- Created `FolderTemplateCatalog.cs`: Static catalog providing `All`, `DefaultTemplate`, `GetTemplate(id, version)`, and `TryGetTemplate(id, version, out descriptor)`.
- Created `FolderTemplateValidator.cs`: Strict validator enforcing `viewBox="0 0 256 256"`, 16px safe boundary, semantic roles, and rejecting disallowed tags (`<script>`, `<foreignObject>`, `<text>`, `<image>`, `<filter>`) and event handler attributes (`on*`).

### B. Renderer Integration (`src/Ornare.Imaging/`)
- Updated `FolderTemplateRenderer.cs`:
  - `CreateRecipe` binds requested `templateId` and `templateVersion` to the deterministic `RenderRecipe`.
  - `RenderColoredTemplateIco` retrieves the template descriptor and delegates multi-resolution ICO rendering to `ImagePipeline.ProcessRecipe`.
  - `RenderPreviewPng` generates bounded 256x256 PNG previews using the requested template and recolored semantic palette.

### C. Quick Panel UI (`src/Ornare.App/`)
- Updated `QuickPanelWindow.xaml`:
  - Added `TemplateRadioButtons` with options for `Classic` (`ornare-classic`), `Soft` (`ornare-soft`), and `Flat` (`ornare-flat`) inside the custom color flyout.
- Updated `QuickPanelWindow.xaml.cs`:
  - Added `_selectedTemplateId` and `_selectedTemplateVersion` tracking.
  - Implemented `OnTemplateSelectionChanged` and `TriggerCustomColorPreview` with 80 ms debouncing.
  - Updated `StageColorAsync` to pass selected template identity into `FolderTemplateRenderer.RenderColoredTemplateIco`.
  - Updated `UserSettings.cs` to persist `DefaultTemplateId` and `DefaultTemplateVersion`.
- Localized in `en-US` and `tr-TR` `Resources.resw`.

---

## 4. Test & Verification Evidence

- **Unit & Integration Tests (`tests/Ornare.Imaging.Tests/FolderTemplateTests.cs`):**
  - Catalog structure and versions: **PASS**
  - Sub-millisecond metadata loading (< 500 µs): **PASS**
  - Fallback and case-insensitive resolution: **PASS**
  - Validator pass on all built-in templates: **PASS**
  - Rejection of invalid viewBox, disallowed tags, script handlers, missing roles, and boundary overflow: **PASS**
  - Exact proportion rules (87.5% and 39.3%): **PASS**
  - Multi-frame ICO rasterization (10 frames) across all templates: **PASS**
  - RMS difference visual distinctness between silhouettes: **PASS**
  - Canonical recipe determinism divergence: **PASS**
- **Full Solution Tests:** 229 tests passed across all test assemblies (`Ornare.Core.Tests`, `Ornare.Storage.Tests`, `Ornare.UiTests`, `Ornare.IntegrationTests`, `Ornare.Imaging.Tests`). 0 failed, 0 skipped.
- **ARM64 Compilation:** `pwsh -File scripts/build-managed-arm64.ps1` succeeded with 0 warnings and 0 errors across all 6 managed projects.
- **End-to-End Real Golden Path:** `pwsh -File scripts/test-real-golden-path.ps1` succeeded with desktop.ini integrity preserved and 0 Explorer restarts.
- **Traceability:** Added `ORN-EXP-F03` row to `docs/REQUIREMENTS_TRACEABILITY.md`.
