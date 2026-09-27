# Ornare — Design References & Open-Source Asset Policy

This document records the official/open projects examined during the folder-template and emblem research phase for Ornare.
In strict accordance with Ornare's non-negotiable asset provenance policy, **all nine production folder style templates are original Ornare artwork created from a blank canvas**. Third-party projects serve strictly as design references or potential emblem sources.

---

## 1. Third-Party Design Reference Projects

### Microsoft Fluent System Icons
- **Upstream Repository:** https://github.com/microsoft/fluentui-system-icons
- **Packages / Artifacts:** `@fluentui/svg-icons`
- **Declared License:** MIT License (https://github.com/microsoft/fluentui-system-icons/blob/main/LICENSE)
- **Role in Ornare:** Design language reference for Windows 11 Fluent aesthetic and potential source for curated vector emblems.
- **Redistribution Basis:** Permissive MIT license with copyright notice. Shipped emblems are audited and verified.
- **Folder Art Status:** Research reference only. No Microsoft Windows folder geometry, paths, or proprietary Explorer assets are copied into Ornare shipping templates.

### Tabler Icons
- **Upstream Repository:** https://github.com/tabler/tabler-icons
- **Declared License:** MIT License (https://github.com/tabler/tabler-icons/blob/master/LICENSE)
- **Role in Ornare:** Clean, highly legible monochrome 24x24 vector icons ideal for center emblems.
- **Redistribution Basis:** Permissive MIT license with copyright notice.

### Lucide Icons
- **Upstream Repository:** https://github.com/lucide-icons/lucide
- **Declared License:** ISC License (https://github.com/lucide-icons/lucide/blob/main/LICENSE)
- **Role in Ornare:** Modern open-source icon set; candidate family for vector emblems.
- **Redistribution Basis:** Permissive ISC license.

### Google Material Design Icons
- **Upstream Repository:** https://github.com/google/material-design-icons
- **Declared License:** Apache License 2.0 (https://github.com/google/material-design-icons/blob/master/LICENSE)
- **Role in Ornare:** Geometric planar reference.
- **Folder Art Status:** Research reference only. No Material folder assets are traced.

### KDE Breeze Icons
- **Upstream Repository:** https://github.com/KDE/breeze-icons
- **Declared License:** LGPL v2.1+
- **Role in Ornare:** Research reference only for geometric character and technical aesthetic (Air style).
- **Folder Art Status:** NO CODE OR SVG PATHS ARE COPIED OR REUSED. Blank-canvas design only.

### GNOME Adwaita Icon Theme
- **Upstream Repository:** https://github.com/GNOME/adwaita-icon-theme
- **Declared License:** LGPL v3 / CC BY-SA 3.0
- **Role in Ornare:** Research reference only for organic curves and body proportions (Grove style).
- **Folder Art Status:** NO CODE OR SVG PATHS ARE COPIED OR REUSED. Blank-canvas design only.

### Apple Design / SF Symbols
- **Status:** **STRICTLY FORBIDDEN**.
- No Apple proprietary folder art, SF Symbols folder resources, or Apple Design Resources may be downloaded, traced, or incorporated into Ornare shipping assets. Aqua style is an independent, blank-canvas design inspired by modern desktop softness.

---

## 2. Legacy Reference Art

### `Windows_11_FOLDER.svg`
- **Location:** `assets-references/legacy/Windows_11_FOLDER.svg`
- **SHA-256:** `6F00228283D5EA7554A93741E41FF193E37210CD52C8150F0A3572CB8491A215`
- **Status:** Retired from production. Retained purely in `assets-references/` for historical tracking.
- **Exclusion:** Explicitly excluded from MSIX packaging, build compilation, and production runtime lookup.

---

## 3. Blank-Canvas Originality Policy

All 9 Ornare folder templates:
1. `ornare-classic` (v2)
2. `ornare-soft` (v2)
3. `ornare-flat` (v2)
4. `ornare-glass` (v1)
5. `ornare-aqua` (v1)
6. `ornare-grove` (v1)
7. `ornare-air` (v1)
8. `ornare-flow` (v1)
9. `ornare-geo` (v1)

are 100% original vectors authored on a 256×256 viewBox with standard semantic roles:
- `data-role="folder-back"`
- `data-role="folder-tab"`
- `data-role="folder-front"`
- `data-role="highlight"`
- `data-role="shadow"`
- `data-role="edge"`
- `data-role="detail"` (optional)

Zero third-party paths, control points, gradient stops, or masks are copied.
