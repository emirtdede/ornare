# F09 Preflight Note — SVG Recoloring

**Feature:** F09 — SVG Recoloring  
**Date:** 2026-09-26  
**Status:** Preflight Complete  

---

## 1. Expected vs Actual Document Map
| Expected Path | Actual Path | Status |
|---|---|---|
| `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md` | `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md` | Exact match |
| `feature-expansion/features/F09_svg-recoloring.md` | `feature-expansion/features/F09_svg-recoloring.md` | Exact match |
| `docs/IMAGE_PIPELINE.md` | `docs/IMAGE_PIPELINE.md` | Exact match |
| `docs/LOGIC_INVARIANTS.md` | `docs/LOGIC_INVARIANTS.md` | Exact match |

All documents are verified present and read in order.

---

## 2. Live Implementation Points & Affected Projects/Files

### `Ornare.Core`
- `ErrorCode.cs`: Add `SvgNotThemeable`, `SvgSecurityViolation`.
- `SemanticSvgRole.cs`: Explicit allowlisted roles: `Base`, `Light`, `Highlight`, `Shadow`, `Deep`, `Badge`.
- `SemanticColorPalette.cs`: Mapping of semantic roles to hex colors with strict parsing.

### `Ornare.Imaging`
- `ImageSecurityPolicy.cs`:
  - Secure SVG preflight validator: XML DTD prohibited, `XmlResolver = null`, rejects external entities, `<!DOCTYPE`, scripts (`<script>`, `onload=`, `javascript:`), `foreignObject`, `http/https/file` URI references, external entities.
- `SvgThemeabilityAnalyzer.cs`:
  - Static XML inspection of safe SVGs.
  - Parse inline `style`, `fill`, `stroke`, `color`, `currentColor`.
  - Classify as `ThemeableSingleColor` if all effective non-`none` solid paints resolve to the same normalized opaque/alpha color.
  - If multiple colors, gradients (without semantic roles), patterns, stylesheets (`<style>`), unresolved variables (`var(...)`), or paint servers (`url(...)`) are found -> return `SvgNotThemeable`.
- `SvgRecolorService.cs`:
  - Built-in semantic template recoloring: matches explicit `data-ornare-role` attributes, replaces fill/stroke or gradient stops with the corresponding role color from `SemanticColorPalette`. Never alters geometry, IDs, or viewBox.
  - User single-color SVG recoloring: replaces the one resolved theme color with the target color while strictly preserving element alpha (`opacity`, `fill-opacity`, `stroke-opacity`, or hex/rgba alpha).
  - Clean XML serialization without external DTD or formatting drift.
- `ImagePipeline.cs`:
  - Integration with `ProcessRecipe`: when `SvgRecolorMode != 0`, execute safe recolor step before rasterization.

---

## 3. Affected Invariants
- **INV-SEC-002**: Malicious/corrupt/oversized image inputs are bounded and rejected.
- **INV-SEC-003**: SVG cannot access network or external file resources.
- **INV-IMG-001**: No supported image input can bypass configured resource limits.
- **INV-IMG-003**: Every ICO size is rendered from normalized source, not recursively from smaller frames.

---

## 4. Schema & Package Implications
- No database schema changes (F18 recipe handles recolor mode/value).
- No new third-party graphics dependencies (SkiaSharp / Svg.Skia prohibited; built-in .NET `System.Xml` + Magick.NET retained).

---

## 5. Measurable Risks & Mitigations
1. **XXE / SSRF / Script Execution**:
   *Mitigation:* DTD prohibited, `XmlResolver = null`, preflight scanner rejects `<script>`, `foreignObject`, `<!DOCTYPE`, `<!ENTITY`, `http://`, `https://`, `file://`.
2. **Visual corruption of multicolor user SVGs**:
   *Mitigation:* Strict `ThemeableSingleColor` analysis: any gradient, pattern, stylesheet, or second color immediately returns typed `SvgNotThemeable`.
3. **Cache fragmentation / collision**:
   *Mitigation:* F18 canonical recipe defines cache identity using source hash + recolor parameters, NOT generated XML bytes.

---

## 6. Baseline Performance
- 98 passed, 0 failed in automated test suite.
- Magick.NET rasterization overhead: ~30-70 ms for multi-frame ICO.
- Recolor / analyzer target: microsecond-scale (< 1 ms per template, benchmark 1,000 templates).
