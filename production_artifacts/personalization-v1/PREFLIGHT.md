# Ornare — Quick Panel Personalization + 9 Folder Style System
## Mandatory Preflight & Architecture Assessment (Phase 1)

- **Date:** 2026-09-27
- **Target Baseline Commit:** `12657149b2a3164986b324850a4b203ac8361964` (main)
- **Status:** APPROVED / PREFLIGHT COMPLETE
- **Automated Test Baseline:** 433 tests across 5 test suites (Core: 64, Storage: 54, Imaging: 179, Integration: 120, UiTests: 16) — 100% PASS, 0 failed, 0 skipped.
- **Artifact:** `production_artifacts/personalization-v1/PREFLIGHT.md`

---

## 1. Executive Overview & First Principles

Ornare is a lightweight, local-only, privacy-first, Windows-native desktop folder customization utility built with WinUI 3, Windows App SDK 2.5.1, .NET 10 LTS, C++ COM shell extension, and Native AOT target.

The product goal remains:
```text
Folder -> choose image OR personalize folder -> preview -> apply -> done
```

The Quick Panel MUST NOT become a dashboard or complex editor. It must preserve its compact dimensions (420 × 330 logical pixels), flat continuous aesthetic without nested cards or decorative gradients, and strict local-only execution.

Two distinct, non-overlapping content modes are established:
1. **Mode A — Custom Image Icon:** The existing golden path (user drops, pastes, or picks an image; contain/cover/fill, padding, background, ICO production).
2. **Mode B — Folder Personalization:** Folder Template (9 styles) + Folder Color + Optional Center Emblem (None | Curated Vector Icon | Emoji).

Filesystem mutation occurs ONLY upon explicit commit via the **Apply** button (`Enter` / `Ctrl+Enter`). All exploration (hover, clicks, color changes, slider adjustments, hex typing) is staged in-memory with debounced preview updates (80 ms). Closing or pressing `Escape` discards unstaged changes without touching disk or `desktop.ini`.

---

## 2. Current Solution Architecture & Process Boundaries

### Explorer Process Boundary (`Ornare.ShellExtension.dll`)
- Invariant **INV-EXP-001** and **INV-EXP-002**: Explorer loads ONLY `Ornare.ShellExtension.dll`.
- Prohibited inside Explorer: SQLite, Magick.NET, emoji renderer, template renderer, gallery data, search index, WinUI, folder mutation logic.
- Shell extension captures the selected folder path, verifies single-target shape, activates/redirects to `Ornare.App.exe` with a compact payload, and returns immediately (< 10 ms).
- **Impact on Shell Extension:** ZERO modifications required for this personalization increment.

### App Process Projects & Responsibilities
- `Ornare.Core`: Immutable models, domain contracts, validation rules, `RenderRecipe`, `FolderEmblemRecipe`, error codes.
- `Ornare.Imaging`: Image decode, SVG security analysis, semantic recoloring (`SvgRecolorService`), OKLab/OKLCH palette generation (`FolderPaletteGenerator`), multi-resolution ICO encoding (`ImagePipeline`), template catalog & rendering (`FolderTemplateRenderer`).
- `Ornare.Storage`: SQLite schema, transactional migrations (currently V1-V6), content-addressed asset tracking, `render_cache` (F18), journal logging, folder assignments/baselines.
- `Ornare.Windows`: Target validation, NTFS folder identity, `desktop.ini` Unicode semantic merge, attribute management, shell notification (`ShellRefreshService`), transactional apply (`ApplyService`).
- `Ornare.App`: WinUI 3 presentation, `QuickPanelWindow`, `ManagerWindow`, keyboard navigation, accessibility, localized resources (`Strings/en-US`, `Strings/tr-TR`).
- `Ornare.Package`: MSIX packaging layout, `Package.appxmanifest`, PRI resources, assets.

---

## 3. Reusable Existing Components

1. **Folder Template Renderer & Recolor Pipeline:**
   - `FolderTemplateRenderer`: High-performance template rendering for preview PNGs and multi-res ICOs.
   - `FolderPaletteGenerator`: OKLab/OKLCH 5-tone deterministic palette generation (`deep`, `shadow`, `light`, `highlight`, `badge`).
   - `SvgRecolorService`: Secure semantic XML recoloring with `data-ornare-role` / `data-role` mapping.
   - `SvgSecurityValidator`: Blocks scripts, foreignObject, external references, DTD entity expansion.

2. **Contrast & Color Science:**
   - `WcagContrast`: WCAG non-text contrast calculation (≥ 3:1) for emblems/badges.
   - `ColorConversion`: Hex, RGB, OKLab, and OKLCH color space conversions.

3. **Render Caching & Deduplication (F18):**
   - `CanonicalRecipeSerializer`: Canonical binary recipe serialization and SHA-256 `render_key` generation.
   - `render_cache` table in SQLite: Zero-decode, zero-render cache hits for previously generated icons.

4. **Transactional Mutation & Recovery Engine:**
   - `ApplyService`: Per-target synchronization lock (`PerTargetSyncLock`), durable journal state transitions (`Prepared` -> `AssetGenerated` -> `FilesystemStaged` -> `FilesystemCommitted` -> `Committed`).
   - `DesktopIniParser`: Non-destructive Unicode `desktop.ini` semantic parser and merger.
   - `FolderCustomizer`: Atomic backup (`folder.prev.ico`), attribute rollback tracking.
   - `ShellRefreshService`: `SHChangeNotify` with `SHCNE_UPDATEITEM` / `SHCNE_UPDATEDIR`.

5. **Placement & Frame Generation:**
   - Multi-resolution frame sizes: [16, 20, 24, 32, 40, 48, 64, 96, 128, 256].
   - F04/F05 status and priority badging compositor: corner badge overlay on frames ≥ 32px.

---

## 4. Exact Existing F03 Template IDs & Migration Policy

Existing live template definitions in `src/Ornare.Imaging/Templates/`:
1. `ornare-classic` (Version: 1)
2. `ornare-soft` (Version: 1)
3. `ornare-flat` (Version: 1)

### Template Versioning Rule (Section 37)
- Stable IDs MUST be preserved: `ornare-classic`, `ornare-soft`, `ornare-flat`.
- Updating the geometry of Classic to original blank-canvas artwork bumps its version from `v1` to `v2`.
- Any existing customized folder with `v1` in its recipe or render cache remains valid on disk and is NOT silently mutated.
- New applies use `v2`.
- The new 6 templates will be registered at Version 1:
  - `ornare-glass` (v1)
  - `ornare-aqua` (v1)
  - `ornare-grove` (v1)
  - `ornare-air` (v1)
  - `ornare-flow` (v1)
  - `ornare-geo` (v1)

---

## 5. Asset Provenance & Windows_11_FOLDER.svg Audit

### Legacy File Audit
- **Target File:** `assets/Windows_11_FOLDER.svg`
- **Issue:** Supply chain provenance and redistribution rights for Microsoft's proprietary Windows 11 Fluent folder asset are unclear.
- **Live References Identified:**
  - `tests/Ornare.Imaging.Tests/FolderTemplateTests.cs` (Line 304: `Windows11FolderTemplateMatchesAssetsSvgAndRendersAccurately` test).
  - `src/Ornare.Imaging/Templates/OrnareClassicTemplate.cs` (doc comment and SVG content).
- **Decoupling Procedure:**
  1. Move `assets/Windows_11_FOLDER.svg` to `assets-references/legacy/Windows_11_FOLDER.svg`.
  2. Exclude `assets-references/` from compilation, output directories, and MSIX packaging.
  3. Re-author `ornare-classic` from a blank canvas as an original Ornare design (version 2) with semantic roles.
  4. Ensure zero build or package dependencies on `Windows_11_FOLDER.svg`.
  5. Author provenance documents for all 9 templates under `assets/folders/provenance/`.

### 9 Original Production Templates:
All 9 templates are original Ornare artwork created from a blank canvas. No third-party SVG paths, masks, or coordinates are copied or traced:
1. `Classic`: Familiar desktop folder silhouette, moderate tab, neutral geometry.
2. `Soft`: Increased corner softness, gentle curves, warm calm silhouette.
3. `Flat`: Minimalist geometry, clean planes, crisp readability.
4. `Glass`: Polished translucent-style accents, restrained highlight system.
5. `Aqua`: Blank-canvas soft desktop styling (zero Apple assets/SF Symbols).
6. `Grove`: Organic, calm, wider visual body (zero GNOME Adwaita tracing).
7. `Air`: Crisp, light, efficient geometric styling (zero KDE Breeze tracing).
8. `Flow`: Precision layered depth, Windows-native utility feel (zero Windows copying).
9. `Geo`: Geometric planar accents, low visual noise, predictable recoloring.

---

## 6. Center Emblem System & Vector Catalog Provenance

### Emblem Model
Unified model across Vector Icon and Emoji:
```csharp
public sealed record FolderEmblemRecipe
{
    public EmblemKind Kind { get; init; } // None, VectorIcon, Emoji
    public string ContentId { get; init; } = string.Empty;
    public string GraphemeSequence { get; init; } = string.Empty;
    public string ForegroundColor { get; init; } = string.Empty; // Auto, hex, or empty
    public double Scale { get; init; } = 1.0;
    public double OffsetX { get; init; } = 0.0;
    public double OffsetY { get; init; } = 0.0;
}
```
Rules:
- Exactly ONE center emblem at a time.
- Selecting VectorIcon clears Emoji.
- Selecting Emoji clears VectorIcon.
- Remove Emblem sets `Kind = None`.
- Composition order: `Template -> Color -> Center Emblem -> Status/Priority Badges (F04/F05)`.
- Center emblem is constrained to the template's `emblemSafeZone` and does not overlap `reservedBadgeZones`.

### Vector Emblem Catalog
- Target: 320 curated monochrome vector icons across 12 categories: General, Work, Development, Documents, Media, Communication, Security, Finance, Education, Gaming, Personal, System.
- Primary Icon Family: Tabler Icons (MIT license) or Lucide (ISC license) or verified Fluent System Icons subset. One coherent set will be chosen, vetted, and recorded in `assets/emblems/manifest.json` and `THIRD-PARTY-NOTICES.md`.
- Lazy-loaded on first open of Icon Flyout (0 ms impact on Quick Panel cold launch).

### Emoji Strategy
- Primary path: Native Windows emoji picker invocation via Win32 / WinUI controlled input.
- Proof of Concept (POC) testing: 20 activation/selection cycles.
- Fallback path: Local CLDR/Unicode-derived Segoe UI Emoji picker with zero network dependency.
- DirectWrite color glyph rendering to premultiplied BGRA surface for final ICO multi-resolution generation.

---

## 7. Affected Invariants

| Invariant | Description | Preservation Strategy |
|---|---|---|
| **INV-EXP-001** | Explorer never loads UI, Magick, SQLite, network | Shell extension remains untouched; 0 new dependencies. |
| **INV-FS-001** | Never delete/overwrite unowned files | Only `.ornare/folder.ico` and owned `desktop.ini` icon semantics are written. |
| **INV-FS-002** | Unrelated `desktop.ini` settings survive | Merged via `DesktopIniParser.MergeSemanticsForApply`. |
| **INV-FS-003** | Reset changes only Ornare-owned semantics | Baseline semantics restored; live file re-read before merge. |
| **INV-TX-001** | Durable journal before first mutation | Journal state `Prepared` written prior to disk writes. |
| **INV-CON-001**| At most one mutation per folder identity | `PerTargetSyncLock` serializes concurrent operations. |
| **INV-IMG-001**| Resource limits enforced | `ImageSecurityPolicy` enforced on all SVG and emblem inputs. |
| **INV-IMG-002**| SVG cannot fetch external network/files | `SvgSecurityValidator` forbids external refs, entities, scripts. |
| **INV-IMG-003**| Every ICO size rendered independently | Normalized vector/source rendered per size (16..256px). |
| **INV-UI-001** | Golden path has no confirmation button | Apply button is the sole commit action; staged changes require explicit click/Enter. |
| **INV-UI-002** | No nested card/dashboard layout | Continuous 420x330px surface; flat action bar; flyouts for options. |
| **INV-PRV-001**| Local-only, no cloud, no telemetry | 100% offline; zero network calls. |

---

## 8. Schema & Storage Impact

- `RenderRecipe`: Extended with `EmblemKind`, `EmblemContentId`, `EmblemGraphemeSequence`, `EmblemForegroundColor`, `EmblemScale`, `EmblemOffsetX`, `EmblemOffsetY`.
- `CanonicalRecipeSerializer`: Updated canonical serialization format (RecipeVersion incremented to ensure cache isolation).
- `render_cache`: Schema remains backward compatible (stores `recipe_json` and `render_key` SHA-256).
- `folder_assignments`: Compatible with existing schema. If new fields are needed, forward transactional migration V7 will be introduced.
- Backward compatibility: Legacy records without emblems deserialize safely with `Kind = None`.

---

## 9. Package & Native AOT Impact

- **MSIX Packaging:**
  - Asset folder `assets/folders/` packaged or embedded as resources.
  - Curated emblem SVG subset packaged under `assets/emblems/`.
  - `assets-references/` strictly excluded.
  - Zero PDBs, test binaries, or source files in MSIX.
- **Native AOT:**
  - `System.Text.Json` source generator context (`FolderJsonContext`) extended for new recipe records.
  - No reflection-based serializers or dynamically loaded assemblies.
  - Native compilation for `win-x64` and `win-arm64` verified with zero warnings.

---

## 10. Performance Baseline & Budget Envelope

Current Gate 3 Baseline (from `production_artifacts/performance/gate-3-performance-report.md`):
- Shell Extension `GetState`: P50 = 0.02 ms, P95 = 0.06 ms (Budget: < 10 ms).
- Quick Panel Cold Launch: P50 = 185 ms, P95 = 240 ms (Budget: < 400 ms).
- Quick Panel Warm Launch: P50 = 42 ms, P95 = 68 ms (Budget: < 150 ms).
- End-to-End Apply Latency: P50 = 68 ms, P95 = 94 ms (Budget: < 500 ms).
- Private Working Set: ~38 MB (Budget: < 180 MB).
- Idle CPU: 0.0%.

New Personalization Budgets:
- Folder flyout first open: <= 100 ms.
- Folder flyout subsequent open: <= 30 ms.
- Icon catalog first open: <= 150 ms (lazy initialized).
- Icon search: P95 <= 50 ms.
- Color preview update after debounce: P95 <= 50 ms.
- Cached template preview: P95 <= 25 ms.
- Repeated >10% regression against certified baseline is strictly blocking.

---

## 11. Preflight Completion Sign-Off

All prerequisites, architectural boundaries, invariants, reusable services, and legacy asset risks have been evaluated and recorded.

Phase 1 Preflight is **COMPLETE**. Ready to execute Phase 2 through Phase 12.
