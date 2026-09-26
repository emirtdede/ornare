# F09 — SVG Recoloring

**Status:** IMPLEMENT  
**Dependencies:** F18  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Provide safe deterministic recoloring of Ornare-controlled semantic SVG templates and narrowly defined user-imported single-color SVGs without adding a second graphics stack.

## Fixed decisions

1. Built-in templates use explicit `data-ornare-role` values from the allowlist `base`, `light`, `highlight`, `shadow`, `deep`, `badge`; color replacement never searches arbitrary hex strings.
2. User SVG recoloring is allowed only after the existing secure-SVG validation succeeds and the document is classified `ThemeableSingleColor`.
3. `ThemeableSingleColor` means: after resolving presentation attributes, inline `style` declarations and inherited `currentColor` within the supported static subset, every effective non-`none` solid `fill`/`stroke` resolves to the same normalized opaque/alpha RGB color. SVGs using multiple paint colors, gradients, patterns, CSS stylesheets, unresolved variables, scripts, `foreignObject`, external resources or paint-server URLs are **not themeable** for recolor and return `SvgNotThemeable`; they may still be imported unchanged if the normal secure SVG pipeline accepts them.
4. Built-in semantic templates may contain gradients only when every gradient stop to be recolored carries an explicit allowlisted `data-ornare-role`; geometry/IDs/viewBox are never inferred from color.
5. Parse XML with DTD prohibited, `XmlResolver = null`, bounded input, existing external-resource rejection and no script execution.
6. F18 canonical recipe identity is based on the original source `AssetHash` + explicit recolor/semantic parameters + renderer/template versions; **serialized recolored SVG bytes are not used as the cache identity**, so XML whitespace/attribute ordering cannot fragment the cache.
7. No SkiaSharp/Svg.Skia dependency unless a later ADR proves a release-blocking limitation in the existing Magick.NET path.

## Integration points

- `Ornare.Imaging`: secure `SemanticSvgTemplate`, `SvgThemeabilityAnalyzer`, `SvgRecolorService`.
- `Ornare.Core`: recolor mode/value objects and typed `SvgNotThemeable`.
- `Ornare.App`: editor calls Imaging; no XML/CSS parsing in UI.

## Implementation sequence

1. Define semantic role enum/allowlist and validate every built-in template at build/test time.
2. Reuse the existing secure SVG preflight before XML recolor; fail before render on disallowed external/script/foreign content.
3. Implement a deliberately small presentation parser for `fill`, `stroke`, `color` and inline `style` declarations required by the themeability contract; do not embed a browser/CSS engine.
4. Implement user themeability analysis exactly according to Fixed Decision 3; unsupported paint constructs produce `SvgNotThemeable`, not guessed output.
5. Recolor built-in templates only by semantic role; recolor user SVGs only by changing the one resolved theme color while preserving original alpha semantics.
6. Serialize valid SVG for the existing Magick.NET raster stage. Deterministic serialization is desirable for fixtures/diffing but is not the F18 identity.
7. Add hostile/complex SVG regression corpus and golden output fixtures.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| XXE/external fetch | DTD prohibited, resolver null, external references/scripts/foreign content rejected by secure preflight. |
| Corrupting multicolor art | Strict `ThemeableSingleColor` classification; otherwise typed rejection for recolor. |
| CSS ambiguity | Support only explicitly defined presentation subset; unresolved/stylesheet-driven paint makes recolor unsupported. |
| Cache fragmentation/collision | F18 canonical recipe, not generated XML bytes, defines identity. |
| ID/gradient breakage | Preserve IDs/viewBox/geometry and change only allowlisted semantic paint attributes/stops. |

## Required tests

- Built-in semantic role recolor changes only expected paint fields.
- Single-color fill/stroke/currentColor fixtures recolor correctly.
- Multiple colors, gradients without semantic roles, patterns, stylesheet paint and unresolved variables return `SvgNotThemeable`.
- External `file/http/https` refs, DTD/entity, script and `foreignObject` are rejected before rasterization.
- Malformed/oversized XML fails without crash/hang.
- For a fixed imported `AssetHash` and fixed recolor recipe, changes to internal XML serialization order/whitespace are not separate render-key inputs; render identity is computed from F18 canonical fields before generated SVG serialization.
- Golden render fixtures pass through Magick.NET.

## Performance requirements

- Recolor/analyzer work must remain small relative to rasterization; benchmark 1000 built-in-template recolors.
- No new native dependency and no browser/WebView/CSS engine.
- No SVG parsing in Explorer.

## Approved sources / research

- **Magick.NET:** https://github.com/dlemstra/Magick.NET
- **Svg.Skia (conditional technical reference only):** https://github.com/wieslawsoltes/Svg.Skia

Before copying any source, re-check exact upstream commit/license and record provenance according to `../01_OSS_SOURCES_AND_LICENSE_POLICY.md`.

## Forbidden implementations

- No regex/global string color replacement.
- No script/foreign object/external network/file resolution.
- No arbitrary multicolor recolor heuristics.
- Do not copy restricted Folder11 assets.

## Feature Definition of Done

- Security/themeability fixtures green.
- Golden-render tests green.
- No new graphics dependency.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F09/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
