# F14 — PNG Export

**Status:** IMPLEMENT  
**Dependencies:** F18,F13 atomic export writer  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Export the current rendered recipe as a high-resolution transparent/solid-background PNG for reuse outside Ornare.

## Fixed decisions

1. Advanced PNG export exposes exactly **256×256, 512×512, and 1024×1024**; **1024×1024 is the default**. Only the selected output size is rendered/cached for that export request; never pre-render all three.
2. Use the same placement/background/template recipe; PNG export must visually match the icon design at large size.
3. PNG encoding uses existing Magick.NET; no System.Drawing.

## Integration points

- `Ornare.Imaging`: derived PNG renderer/encoder.
- `Ornare.App`: SaveFilePicker plus required three-choice size selector (256/512/1024, default 1024).
- `Ornare.Core`: export command.

## Implementation sequence

1. Add PNG target size to derived render recipe/cache key.
2. Render directly from normalized source/vector recipe at target size; do not upscale a 256px ICO frame.
3. Encode PNG with alpha when background is transparent.
4. Reuse F13 `AtomicExportWriter` exactly: sibling temp + validation + `ReplaceFileW` for existing local destination or `MoveFileExW(..., MOVEFILE_WRITE_THROUGH)` for new local destination; unsupported provider semantics return `UnsupportedExportDestination`.
5. Round-trip decode exported PNG in tests to verify dimensions/alpha.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| Blurry export | Render at target resolution from source/vector, never upscale ICO. |
| Memory spike | 1024 canvas bounded; dispose immediately after encode. |
| Different appearance | Share recipe/placement/palette code. |

## Required tests

- 256/512/1024 dimensions and alpha.
- Visual golden comparison with recipe preview.
- Failure/cancel behavior.
- Cache-hit repeat export.

## Performance requirements

- 1024 render on demand only; never pre-render every asset.
- Dispose bitmap immediately.

## Approved sources / research

- **Magick.NET:** https://github.com/dlemstra/Magick.NET

Before copying any source, the agent must re-check the exact upstream commit/license and record provenance according to `../01_OSS_SOURCES_AND_LICENSE_POLICY.md`.

## Forbidden implementations

- No System.Drawing/GDI+ encoder.
- No pre-generated PNG cache for every library item unless explicitly requested.

## Feature Definition of Done

- Round-trip/golden tests green.
- Memory benchmark acceptable.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F14/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
