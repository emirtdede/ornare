# F20 — Folder Drag & Drop into Manager

**Status:** IMPLEMENT  
**Dependencies:** F15,F16,F17,F19; existing target eligibility resolver  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Allow one or many filesystem folders dragged from Explorer into Manager to become a temporary validated working selection without blocking UI, creating false customization records or mutating any folder.

## Fixed decisions

1. Use Windows App SDK/WinUI drag-and-drop APIs only; no third-party drag/drop framework.
2. A drop creates an **ephemeral Manager Target Tray/staging selection**. Drop alone creates no persisted “Customized Folder” row, no history/journal row and no folder mutation.
3. Existing Customized Folders records remain the source of persisted customization state; they are created/updated only by successful existing customization operations.
4. If a dropped folder identity already matches a known/customized record, the staging item **must reference that existing record ID** and must not create a duplicate staging/ownership record.
5. Closing Manager, clearing Target Tray, or starting a fresh session discards the uncommitted staging selection.
6. Maximum input per single drop is **1000 items**. Reject the entire drop before deep validation if raw item count exceeds 1000.
7. Duplicates are coalesced first by the F19 qualified `(VolumeSerialNumber, FileId64)` identity for eligible NTFS targets; for items that are rejected before an identity can be obtained, duplicate rejected entries are coalesced only by the existing canonical-path normalizer.
8. Validation concurrency is fixed at **4**; validation is cancellable.

## Integration points

- `Ornare.App`: Target Tray/staging view model, WinUI drop target, async progress/cancel.
- `Ornare.Windows`: target eligibility/identity resolver reused read-only.
- `Ornare.Core`: accepted/rejected staging result model.

## Implementation sequence

1. Add `AllowDrop` only to the intended Manager Target Tray/surface and accept StorageItems/folders in `DragOver`.
2. On Drop, obtain items asynchronously; if raw count >1000, return localized limit result before per-item filesystem work.
3. Reject non-folders and resolve target eligibility/identity off the UI thread with bounded concurrency exactly 4.
4. Coalesce duplicate identities/paths and produce ordered accepted/rejected results.
5. Populate only the in-memory staging collection. No SQLite write is permitted solely because of Drop.
6. For already-customized accepted targets, attach the existing record ID as read-only contextual metadata.
7. Present rejected count with expandable reasons; no modal-per-item spam.
8. UI list is virtualized; cancellation stops remaining validation and retains only items whose validation completed successfully before cancellation, clearly marked as partial staging.
9. Any later Apply/Reset/etc. starts a normal operation and revalidates target immediately before commit.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| Drop creates phantom customized rows | Explicit no-persistence contract; DB snapshot test. |
| UI freeze on hundreds of folders | Async work + concurrency=4 + virtualization; no UI-thread filesystem loop. |
| Unsafe target accepted | Reuse eligibility resolver and revalidate at commit. |
| Duplicate aliases | Identity-first coalescing; path fallback only when identity unavailable. |
| Cancel ambiguity | Completed valid staging items remain; unfinished items are not silently accepted. |

## Required tests

- 1, 100, 1000 folders accepted path; >1000 raw items rejected before deep validation.
- Mixed files/folders/unsupported/reparse/network targets.
- Duplicate path aliases and same identity coalesce.
- Already-customized folder links to existing record without duplicate DB row.
- DB/filesystem/journal snapshots prove Drop alone causes zero durable mutation.
- Cancel during validation produces deterministic partial staging.
- Manager close/clear discards staging.
- No >100 ms UI-thread stall in large-drop test.

## Performance requirements

- Concurrency exactly 4 unless a future evidence-backed ADR changes it.
- No synchronous per-item filesystem operations on UI thread.
- Staging list is virtualized/incremental.
- No Quick Panel or Explorer startup impact.

## Approved sources / research

- **Windows drag and drop:** https://learn.microsoft.com/en-us/windows/apps/develop/data/drag-and-drop
- **Crystal Folders reference:** https://github.com/genesistoxical/crystal-folders

## Forbidden implementations

- No recursive subfolder enumeration.
- No auto-apply on drop.
- No persistence merely for staging.
- No third-party drag/drop framework.

## Feature Definition of Done

- Large-drop responsiveness and zero-mutation evidence retained.
- Staging lifecycle/duplicate/existing-record tests green.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F20/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
