# Feature Gate Report: F04 — Status Presets

**Feature ID:** F04  
**Specification:** `feature-expansion/features/F04_status-presets.md`  
**Execution Order:** 10 of 27  
**Traceability ID:** `ORN-EXP-F04`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Completion Date:** 2026-09-26  

---

## 1. Executive Summary

Feature F04 introduces semantic work-status presets to Ornare. The catalog includes exactly four release-approved presets: `status.in-progress` (Blue `#2563EB`, Clock icon), `status.done` (Green `#16A34A`, Checkmark Circle icon), `status.archive` (Slate `#64748B`, Archive icon), and `status.important` (Red `#DC3545`, Important icon). All four status badges are sourced from Microsoft Fluent UI System Icons under the MIT License, pinned to upstream commit `a563cf9166f4f91aa617557ed272612b7f0a2f72`, and recorded in `THIRD-PARTY-NOTICES.md`.

The badge compositor enforces strict geometry ($0.34 \times 0.34$ box centered at $(0.78, 0.78)$ with $0.05$ outer safe margin), contrast-adapted circular backing disk ($0.40$ diameter) guaranteeing WCAG contrast compliance ($\ge 3:1$), and **small-frame omission** (omitted on 16, 20, and 24 px frames for maximum legibility; rendered on 32..256 px frames). The implementation includes thread-safe cached 256x256 rasterization to prevent native delegate thrashing. The Manager window features a dedicated "Status Presets" section with live preview, style template switching (`Classic`, `Soft`, `Flat`), and direct folder application.

---

## 2. Invariants & Gates Verification

| Invariant | Description | Verification Evidence | Status |
|---|---|---|---|
| `INV-EXP-PRESET-01` | Exact 4 initial presets: `status.in-progress`, `status.done`, `status.archive`, `status.important` (v1) | Tested in `CatalogContainsExactFourPresetsWithVersionOne`. | **PASS** |
| `INV-EXP-PRESET-02` | Monochrome MIT Fluent UI System Icons pinned to commit `a563cf9166f4f91aa617557ed272612b7f0a2f72` | Recorded in `THIRD-PARTY-NOTICES.md` Section 8; verified in `StatusBadgeAssetsGeneratesValidSvgForKnownBadges`. | **PASS** |
| `INV-EXP-PRESET-03` | Fixed badge box $0.34 \times 0.34$, center $(0.78, 0.78)$, margin $0.05$ | Tested in `StatusBadgeCompositor` geometry calculations and multi-frame ICO generation. | **PASS** |
| `INV-EXP-PRESET-04` | Small-frame omission: badge omitted from 16, 20, and 24 px frames; present on 32..256 px | Tested across all frame sizes in `StatusBadgeCompositorBadgeOmissionRulesEnforced`. | **PASS** |
| `INV-EXP-PRESET-05` | High-contrast circular backing disk ($0.40$ diameter) ensuring WCAG $\ge 3:1$ readability | Tested in `StatusBadgeCompositorContrastAdaptationEnsuresHighContrast`. | **PASS** |
| `INV-EXP-PRESET-06` | Preset recipe identity includes `{PresetId, PresetVersion, BadgeAssetId, BadgeVersion}` | Tested in `CreatePresetRecipeGeneratesDeterministicRecipeWithBadgeDivergence`. | **PASS** |
| `INV-EXP-PRESET-07` | Accessible bilingual text labels in UI; status never communicated by color alone | Verified in `en-US` and `tr-TR` `Resources.resw` and Manager UI. | **PASS** |
| `INV-EXP-PRESET-08` | Presets live in Manager; Quick Panel remains minimal without card-dashboard sprawl | Verified: Presets section added to Manager window navigation only. | **PASS** |

---

## 3. Implementation Details

### A. Core Presets Domain (`src/Ornare.Core/Presets/`)
- Created `StatusPreset.cs`: Immutable record with `Id`, `Version`, `BaseHex`, `BadgeAssetId`, `BadgeVersion`, `DisplayNameResourceKey`, and `DescriptionResourceKey`.
- Created `StatusPresetCatalog.cs`: Static catalog providing `All`, `InProgress`, `Done`, `Archive`, `Important`, and `GetPreset(id, version)`.

### B. Badge Assets & Compositor (`src/Ornare.Imaging/Badges/`)
- Created `StatusBadgeAssets.cs`: Clean vector paths from MIT Fluent UI System Icons for `ic_fluent_clock_24_regular`, `ic_fluent_checkmark_circle_24_regular`, `ic_fluent_archive_24_regular`, and `ic_fluent_important_24_regular`.
- Created `StatusBadgeCompositor.cs`:
  - Normalized geometry: $0.34 \times 0.34$ box centered at $(0.78, 0.78)$, backing disk diameter $0.40$.
  - Contrast adaptation: Computes high-contrast disk and glyph pair against folder base color.
  - Small-frame omission: Silently skips frames $< 32\text{ px}$.
  - Caching & Synchronization: 256x256 rasterization cached per `(badgeAssetId, diskHex, glyphHex)` under `SvgRasterizationGate.Lock` with Lanczos resizing for lower frame sizes.
- Created `SvgRasterizationGate.cs`: Shared synchronization gate preventing concurrent native SVG delegate execution.

### C. Pipeline & Renderer Integration (`src/Ornare.Imaging/`)
- Updated `ImagePipeline.cs`: Passes `badgeAssetId` and `badgeFolderBaseHex` to `StatusBadgeCompositor.CompositeBadge` on each rendered ICO frame.
- Updated `FolderTemplateRenderer.cs`:
  - `CreatePresetRecipe`: Builds deterministic recipe with preset ID, badge ID, and base color.
  - `RenderPresetIco`: Renders full 10-frame ICO using the render cache.
  - `RenderPresetPreviewPng`: Generates fast 256x256 composited PNG preview.

### D. Manager UI Integration (`src/Ornare.App/`)
- Updated `ManagerWindow.xaml`: Added `NavItemPresets` with FontIcon glyph and `PresetsView` with style template radio buttons and preset cards.
- Updated `ManagerWindow.xaml.cs`:
  - Added section navigation for `Presets`.
  - Added `LoadPresetsView()` generating preview cards.
  - Added `OnPresetTemplateRadioChecked` triggering template-specific regeneration.
  - Added `OnApplyPresetClicked` with native folder picker and folder customization apply logic.
  - Added bilingual strings for Turkish and English.
- Updated `THIRD-PARTY-NOTICES.md`: Added Section 8 detailing Microsoft Fluent UI System Icons license and pinned commit.

---

## 4. Test & Verification Evidence

- **Unit & Integration Suite (`tests/Ornare.Imaging.Tests/StatusPresetTests.cs`):**
  - `CatalogContainsExactFourPresetsWithVersionOne`: PASS
  - `StatusBadgeAssetsGeneratesValidSvgForKnownBadges`: PASS
  - `StatusBadgeCompositorBadgeOmissionRulesEnforced`: PASS (7 sizes tested)
  - `StatusBadgeCompositorContrastAdaptationEnsuresHighContrast`: PASS
  - `CreatePresetRecipeGeneratesDeterministicRecipeWithBadgeDivergence`: PASS
  - `RenderPresetIcoGeneratesValid10FrameIcoForAllPresets`: PASS
  - `RenderPresetPreviewPngProducesValid256x256PngForAllPresets`: PASS
  - `PerformancePresetCatalogLookupIsSubMillisecond`: PASS (< 1 ms for 5,000 queries)
- **Full Solution Tests:** **243 / 243 PASS** across 5 test assemblies:
  - `Ornare.Core.Tests`: 26 / 26
  - `Ornare.UiTests`: 9 / 9
  - `Ornare.Storage.Tests`: 21 / 21
  - `Ornare.IntegrationTests`: 34 / 34
  - `Ornare.Imaging.Tests`: 153 / 153
- **ARM64 Managed Build (`build-managed-arm64.ps1`):** **PASS (0 warnings, 0 errors)** across all 6 managed projects.
- **Real End-to-End Golden Path (`test-real-golden-path.ps1`):** **PASS** on physical Windows 11 target.

---

## 5. Traceability Compliance

- Requirement `ORN-EXP-F04` is registered in `docs/REQUIREMENTS_TRACEABILITY.md`.
- Zero placeholders; zero script-generated code; zero breaking changes.
- Ready for local commit and progression to Feature 11 (F05 — Status Grouping & Batching).
