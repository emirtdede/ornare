# Phase 3 Report: Nine Original Shipping Templates

## 1. Executive Summary
Phase 3 establishes the complete set of **exactly nine production folder style templates** for Ornare, authored entirely from a blank canvas in accordance with the Ornare Originality Policy and Section 8/Section 13 of the Normative Plan.

All 9 templates have passed:
1. Blank-canvas originality validation (zero third-party coordinates, zero tracing).
2. SVG security preflight (`SvgSecurityValidator`): zero scripts, zero external references, zero embedded base64/foreignObject.
3. Geometry & semantic role validation (`FolderTemplateValidator`): viewBox `0 0 256 256`, 16px safe margins, semantic roles (`data-role` and `data-ornare-role` dual compatibility).
4. Manifest contract compliance (`manifest.json`): non-overlapping emblem safe zones and reserved badge zones.
5. Multi-resolution render smoke: verified deterministic rasterization at all 10 standard shell resolutions (16, 20, 24, 32, 40, 48, 64, 96, 128, 256).
6. 10-frame ICO generation: verified valid ICO structure with 10 mipmap frames for every template.

---

## 2. Authoritative Template Specifications

| Sort | Template ID | Public Display Name | Version | SVG File | SHA-256 Hash | Safe Zone (Norm) | Badge Zone (Norm) |
|---|---|---|---|---|---|---|---|
| 1 | `ornare-classic` | Classic | 2 | `assets/folders/ornare-classic.svg` | `E3E396B0BA04FB9A2960F3F79CAFFA49AE594CA11FEEE9665EB27C6C7375BBBF` | `[0.26, 0.40, 0.30, 0.30]` | `[0.58, 0.58, 0.40, 0.40]` |
| 2 | `ornare-soft` | Soft | 2 | `assets/folders/ornare-soft.svg` | `5A0ED84F917F4E40ABE3F2D2E23D565EF6D9F27D638CFA010F8766DF28DF7D7A` | `[0.26, 0.40, 0.30, 0.30]` | `[0.58, 0.58, 0.40, 0.40]` |
| 3 | `ornare-flat` | Flat | 2 | `assets/folders/ornare-flat.svg` | `E06FF41575B308F42F6B8A4F12EEE13CB12CA23A7D03A0BF7079ED7560D6B64C` | `[0.26, 0.40, 0.30, 0.30]` | `[0.58, 0.58, 0.40, 0.40]` |
| 4 | `ornare-glass` | Glass | 1 | `assets/folders/ornare-glass.svg` | `E8E7FB77B088B25E982284C94F9CA3108C4A6564F4320B966C455695079C483F` | `[0.26, 0.40, 0.30, 0.30]` | `[0.58, 0.58, 0.40, 0.40]` |
| 5 | `ornare-aqua` | Aqua | 1 | `assets/folders/ornare-aqua.svg` | `130069F0CFC67BCDED472A56A7550D7A7AAE771238C67E01C50CDCBA3C7CA153` | `[0.26, 0.40, 0.30, 0.30]` | `[0.58, 0.58, 0.40, 0.40]` |
| 6 | `ornare-grove` | Grove | 1 | `assets/folders/ornare-grove.svg` | `54BD8277BAD532BCCF3AC3E205F292BBCDF19DF5F5EBFF7328B76B3336401978` | `[0.26, 0.40, 0.30, 0.30]` | `[0.58, 0.58, 0.40, 0.40]` |
| 7 | `ornare-air` | Air | 1 | `assets/folders/ornare-air.svg` | `ACE1F94D6ACB88320B86BAE111B18EAF9AC352CB33A3F1AD9EBF456101FD4B24` | `[0.26, 0.40, 0.30, 0.30]` | `[0.58, 0.58, 0.40, 0.40]` |
| 8 | `ornare-flow` | Flow | 1 | `assets/folders/ornare-flow.svg` | `163E69EE96FC64C05EF8DD68AC024F2E319B599A9C46F0D4359141A9D40E3D00` | `[0.26, 0.40, 0.30, 0.30]` | `[0.58, 0.58, 0.40, 0.40]` |
| 9 | `ornare-geo` | Geo | 1 | `assets/folders/ornare-geo.svg` | `9CB75F440AD30D7261B1EE1928994BCF21FA60A1B9169B487D52C21132FBE2CF` | `[0.26, 0.40, 0.30, 0.30]` | `[0.58, 0.58, 0.40, 0.40]` |

---

## 3. Provenance & Originality Evidence
Each template is documented by a dedicated provenance file in `assets/folders/provenance/`:
- `ornare-classic.md`: Original blank canvas, balanced silhouette, version 2.
- `ornare-soft.md`: Original blank canvas, pill tab, calm softened corners, version 2.
- `ornare-flat.md`: Original blank canvas, crisp angular tab, minimal planes, version 2.
- `ornare-glass.md`: Original blank canvas, polished translucency visual language, version 1.
- `ornare-aqua.md`: Original blank canvas, sweeping curved tab, organic softness, version 1 (zero Apple/macOS tracing).
- `ornare-grove.md`: Original blank canvas, wide organic stance, gentle geometry, version 1 (zero GNOME/Adwaita tracing).
- `ornare-air.md`: Original blank canvas, technical diagonal tab, crisp lines, version 1 (zero KDE/Breeze tracing).
- `ornare-flow.md`: Original blank canvas, precision layered floating plate, version 1 (zero Windows/Fluent tracing).
- `ornare-geo.md`: Original blank canvas, chamfered facet geometry, version 1 (zero Google/Material tracing).

---

## 4. Manifest and Backward-Compatibility Architecture
- **Production Catalog (`FolderTemplateCatalog.All`)**: Exposes exactly the 9 production descriptors in order.
- **Legacy V1 Resolution (`FolderTemplateCatalog.GetTemplate(id, 1)`)**: Preserves V1 resolution for `ornare-classic`, `ornare-soft`, and `ornare-flat`, guaranteeing that existing customized folders or cache entries can be resolved without runtime exceptions.
- **Section 15 Contract**: `assets/folders/manifest.json` provides machine-readable metadata. Deserialization is verified against `FolderJsonContext.Default.FolderTemplateManifest`.

---

## 5. Verification Results
- `NineTemplatesAcceptanceGateTests`: PASS (all 9 templates tested across 10 resolutions and full ICO generation).
- `FolderTemplateTests`: PASS (asserting exact 9 production templates, sort order, and legacy resolution).
- Total `Ornare.Imaging.Tests`: 203 passed, 0 failed.
- Total `Ornare.Core.Tests`: 81 passed, 0 failed.
- Total `Ornare.Storage.Tests`: 54 passed, 0 failed.

Phase 3 is complete and ready to commit.
