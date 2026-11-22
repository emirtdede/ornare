# Ornare Vector Emblem Catalog - Asset Provenance & Licensing

## 1. Overview
This document records the provenance, licensing, curation criteria, and security audit for the 320 monochrome vector emblem assets shipped with Ornare Quick Panel Personalization (Phase 6 / Section 18 & 19).

## 2. Upstream Source Information
- **Source Project:** Microsoft Fluent UI System Icons (`@fluentui/svg-icons`)
- **Repository:** https://github.com/microsoft/fluentui-system-icons
- **Upstream Package:** `@fluentui/svg-icons@1.1.343`
- **Release/Version:** `1.1.343`
- **License:** MIT License
- **Copyright:** Copyright (c) Microsoft Corporation. All rights reserved.

## 3. License Grant & Compatibility
The upstream package is distributed under the standard MIT License, granting permission to use, copy, modify, merge, publish, and distribute copies of the software without restriction, subject to including the copyright notice and permission notice.

## 4. Curated Catalog Structure
To prevent runtime bloat and adhere to the normative contract, Ornare does NOT bundle the entire 24,000+ asset library. Instead, exactly **320 curated monochrome vector icons** are shipped across 12 distinct functional categories:

| Category | Icon Count | Description / Domain |
|---|---|---|
| `general` | 27 | Core utility, navigation, action, and priority icons |
| `work` | 27 | Project management, office, calendar, tasks, presentation |
| `development` | 27 | Coding, git/branches, cloud, containers, terminal, architecture |
| `documents` | 27 | File types, formats, certificates, archiving, text notes |
| `media` | 27 | Audio, video, photography, playback, streaming |
| `communication` | 27 | Mail, chat, messaging, contacts, community |
| `security` | 27 | Locks, shields, credentials, privacy, authentication |
| `finance` | 27 | Currency, payments, banking, shopping, accounting |
| `education` | 26 | Academics, science, research, mathematics, literature |
| `gaming` | 26 | Gaming controllers, achievements, sports, entertainment |
| `personal` | 26 | Daily life, travel, home, weather, hobbies, wellness |
| `system` | 26 | Hardware, devices, connectivity, storage, operating system |
| **Total** | **320** | **100% Curated, Coherent Fluent Family** |

## 5. Security & Invariant Audit
All 320 SVG assets were subjected to the Ornare static security audit:
1. **Zero Scripts:** Audited for `<script>` tags, event handlers (`on...`), or embedded JavaScript (none found).
2. **Zero External References:** Audited for `xlink:href`, `href`, `url()`, `@import`, or external font/CSS fetches (none found).
3. **Zero Embedded Foreign Content:** No `<foreignObject>`, base64 raster blobs, or HTML fragments.
4. **Normalized Coordinate System:** Uniform `viewBox="0 0 24 24"` with `width="24"` and `height="24"`.
5. **Monochrome Recoloring:** Standardized to `<path fill="currentColor" d="..."/>` enabling direct recoloring via adaptive contrast (F10 `WcagContrast`), white, black, or custom hex values.
6. **Integrity Verification:** SHA-256 digests are computed for all 320 assets and permanently recorded in `assets/emblems/manifest.json`.

## 6. Composition & Safe-Zone Rules
Vector emblems are composited into folder templates according to the Section 17 & 20 contract:
- Center emblem safe zone: `X=0.26, Y=0.40, W=0.30, H=0.30` (normalized relative to template viewBox).
- Zero overlap with reserved badge zones (`X=0.58, Y=0.58, W=0.40, H=0.40`).
- Fully compatible with F04/F05 status and priority corner overlays.
