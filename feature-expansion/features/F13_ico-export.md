# F13 — ICO Export

**Status:** IMPLEMENT  
**Dependencies:** F18,F12  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Export the selected asset/style recipe as a verified multi-resolution `.ico` while reusing an identical cached render whenever possible and guaranteeing that a failed export does not leave a partially overwritten destination.

## Fixed decisions

1. Exported ICO contains exactly the qualified frame set `16,20,24,32,40,48,64,96,128,256`.
2. If F18 has a verified cache artifact for the exact render recipe/frame policy, export copies those bytes; otherwise it renders once, validates, caches, then exports.
3. `SaveFilePicker` is the only GUI destination chooser. Cancel performs zero writes.
4. Atomic export is required for filesystem-backed destinations: create a uniquely named sibling temp file in the destination directory, write + flush + validate it, then commit with Windows replace/move semantics. Do not stream directly over an existing destination.
5. If the picker returns a destination that cannot provide the filesystem semantics needed for the atomic contract, return typed `UnsupportedExportDestination` rather than silently weakening durability.
6. Exported user paths are not canonical Library asset identities and are never inserted into the asset table solely because export occurred.

## Integration points

- `Ornare.Imaging`: existing ICO encoder/validator only.
- `Ornare.Core`: `ExportIcoRequest/Result`.
- `Ornare.App`: HWND-initialized `SaveFilePicker`.
- `Ornare.Windows`: shared `AtomicExportWriter` using sibling temp + replace/move.
- `Ornare.Storage`: render-cache lookup only; no user-export-path asset row.

## Implementation sequence

1. Resolve the canonical render request and F18 `RenderKey`.
2. Reuse a verified cached ICO or create/validate it through the existing renderer.
3. Open `SaveFilePicker` using the app's existing HWND initialization pattern; filter/default extension is `.ico`.
4. After the picker returns a local filesystem-backed destination, perform the same-directory stale-temp cleanup rule from the risk table, then create a sibling temp named exactly `.ornare-export-{GUID}.tmp` using a newly generated GUID and `CREATE_NEW` semantics so an existing file is never reused.
5. Copy cached/rendered bytes to the temp file, flush file data, reopen/validate size/header/frame set.
6. If destination existed, commit with `ReplaceFileW(destination, temp, null, 0, ...)`; if that local filesystem/provider cannot satisfy `ReplaceFileW`, abort with `UnsupportedExportDestination` and preserve the old destination. If destination did not exist, publish the sibling temp with `MoveFileExW(temp, destination, MOVEFILE_WRITE_THROUGH)`. Never fall back to copy-over/truncate for this feature. The helper deletes only Ornare's own temp on failure.
7. Surface typed access/full-disk/unsupported-destination errors; never claim success until final destination reopens and validates.
8. Do not import the export path back into Library automatically.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| Existing file corrupted on failure | Never write in place; sibling temp + validated final commit. |
| Cloud/provider destination lacks atomic filesystem semantics | Fail with `UnsupportedExportDestination` in this version. |
| Re-render waste | Exact F18 cache hit copies bytes only. |
| Export differs from applied icon | Same canonical render recipe and exact frame policy. |
| Temp leak after crash | Temp name is exactly `.ornare-export-{GUID}.tmp`. No global/startup directory scan is allowed. At the start of a later export to the **same selected destination directory**, enumerate only this filename pattern, attempt deletion only for files older than 24 hours, skip any file that cannot be opened with delete sharing/whose attributes indicate it is in use, and record nonfatal cleanup failures in redacted diagnostics; never scan or delete arbitrary files. |

## Required tests

- Export/re-import round-trip verifies all ten frames.
- Cancel creates no file/temp/DB mutation.
- Existing destination replacement success.
- Injected failure before commit preserves original destination byte-for-byte.
- Read-only/full-disk/access-denied failures leave no corrupt final file.
- Cache-hit export is byte-identical to cache artifact.
- Non-filesystem/unsupported provider destination returns typed failure rather than direct destructive write.

## Performance requirements

- Cache-hit export is bounded file copy + validation only.
- No startup work and no Explorer work.
- Temp/validation uses streaming/bounded memory.

## Approved sources / research

- **Magick.NET:** https://github.com/dlemstra/Magick.NET
- **ReplaceFileW:** https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew
- **MoveFileExW:** https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefileexw

Before copying source, re-check exact upstream commit/license and record provenance according to `../01_OSS_SOURCES_AND_LICENSE_POLICY.md`.

## Forbidden implementations

- No second ICO encoder.
- No direct overwrite/truncate of an existing destination.
- No automatic Library import of export path.
- No false atomicity claim for unsupported provider destinations.

## Feature Definition of Done

- Round-trip, atomic-replacement and injected-failure tests green.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F13/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
