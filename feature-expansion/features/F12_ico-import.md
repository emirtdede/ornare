# F12 — ICO Import

**Status:** IMPLEMENT  
**Dependencies:** F18  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Import existing `.ico` files safely, preserve compliant multi-frame icons when possible, and normalize only when required.

## Fixed decisions

1. ICO goes through explicit parser/decoder validation; file extension alone is not trusted.
2. Fast path is allowed only when the ICO decodes successfully, every frame is square, each dimension is `1..256`, frame/resource limits are respected, and the ICO contains at least one valid image for **every** required Ornare size `16,20,24,32,40,48,64,96,128,256`; extra valid frames are permitted.
3. Otherwise normalize. For each required target size choose source deterministically: exact-size valid frame first; else the smallest valid frame whose dimension is greater than the target; else the largest valid frame. Render each target independently from that chosen source frame through the existing encoder; never resize from a previously generated target frame.
4. Canonical imported asset is stored by SHA-256; derived normalized ICO has separate render key.

## Integration points

- `Ornare.Imaging`: ICO validator/import normalizer using Magick.NET/current encoder.
- `Ornare.Core`: typed import results/errors.
- `Ornare.App`: file picker/filter and Library import route.

## Implementation sequence

1. Add `.ico` to the existing import command rather than a second picker flow.
2. Validate magic/container through decoder, frame count/dimensions, decoded memory and alpha; reject malformed bombs safely.
3. If input satisfies the exact fast-path rule in Fixed Decision 2, store canonical bytes and mark `ImportKind=IcoCompliant`; otherwise create a normalized derived ICO using the deterministic frame-selection rule in Fixed Decision 3.
4. Thumbnail/preview uses decoded bounded frame.
5. Applying imported ICO still copies a folder-owned `.ornare\folder.ico`; never point desktop.ini to the external source path.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| Malformed ICO parser bugs | Use Magick.NET plus hostile corpus; never write a custom binary parser unless a proven decoder gap exists. |
| Source deletion breaks folder | Always use folder-owned durable copy. |
| Huge/many frames | Existing resource limits + explicit frame count/dimension limits. |
| Wrong frame chosen | Test Windows-standard frame selection and regenerate normalized set when needed. |

## Required tests

- Valid single/multi-frame ICO.
- Corrupt headers/truncated/oversized frame corpus.
- Source ICO deleted after apply: folder remains customized.
- Import same ICO twice: dedup hit.
- Real x64 decoder/import/package smoke; ARM64 decoder path must compile/package/AOT with correct native architecture, while physical ARM64 decoder runtime remains `NOT PROVEN / PENDING HARDWARE CERTIFICATION` until hardware is available.

## Performance requirements

- Fast path avoids rerender where safe.
- Bounded preview decode.

## Approved sources / research

- **Magick.NET:** https://github.com/dlemstra/Magick.NET
- **Drop Icons reference:** https://github.com/genesistoxical/drop-icons
- **ImageToIcon reference only:** https://github.com/koryboc/ImageToIcon

Before copying any source, the agent must re-check the exact upstream commit/license and record provenance according to `../01_OSS_SOURCES_AND_LICENSE_POLICY.md`.

## Forbidden implementations

- No hand-written ICO parser by default.
- No external source path in desktop.ini.
- Do not copy GPL ImageToIcon source.

## Feature Definition of Done

- Hostile corpus green.
- Persistence/dedup green.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F12/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
