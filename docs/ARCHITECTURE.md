# Software Architecture Specification

## 1. Architecture goal

Keep Explorer integration minimal and isolated; keep application responsibilities modular without turning a small desktop utility into an enterprise framework.

## 2. Process boundaries

### Explorer process

`Ornare.ShellExtension.dll` is the only Ornare code loaded by Explorer.

Allowed responsibilities:

- validate that exactly one supported folder is selected;
- expose title/icon/state for the command;
- obtain the selected folder safely;
- activate/redirect to the packaged app with a minimal command payload;
- return promptly.

Forbidden inside Explorer:

- WinUI initialization;
- Magick.NET;
- SQLite;
- network access;
- image decoding;
- ICO generation;
- repair scans;
- heavy logging;
- synchronous disk traversal;
- managed runtime unless explicitly proven safe and justified (default: no).

### App process

The app owns UI, validation, imaging, folder customization, DB, history, repair, diagnostics and settings.

## 3. Proposed solution projects

- `Ornare.App` - packaged WinUI 3 application, activation, quick panel, manager shell.
- `Ornare.DesignSystem` - tokens and reusable WinUI controls.
- `Ornare.Core` - domain contracts, operations, validation, result/error models.
- `Ornare.Imaging` - secure decode/normalize/render/ICO pipeline.
- `Ornare.Storage` - SQLite metadata and canonical asset files. Use `Microsoft.Data.Sqlite.Core` with `SQLitePCLRaw.bundle_winsqlite3`, which targets Windows system `winsqlite3.dll`. Initialize the bundle before first storage use as required by the selected package/version, verify the effective native library/version at Gate 1, and validate x64/ARM64 + Native AOT. Do not depend on optional SQLite extensions unless qualification proves them present on every supported baseline.
- `Ornare.Windows` - Windows folder customization APIs, file attributes, shell refresh, platform helpers.
- `Ornare.ShellExtension` - native C++ COM implementation.
- `Ornare.Package` - MSIX manifest/assets/package registration.

Test projects mirror behavior domains without unnecessary one-to-one project duplication.

## 4. Activation

Use package identity and a supported activation path so the shell extension can launch or redirect to the main application. Main app is single-instance for the quick workflow. New folder invocations redirect arguments to the active instance.

The activation payload contains only what is necessary; avoid complex serialized state. The app revalidates the path and filesystem state after activation.

## 5. Domain operation model

Core operations should be explicit:

- `ApplyFolderIcon`
- `ResetFolderIcon`
- `UndoFolderIconChange`
- `RepairFolderCustomization`
- `ImportIconAsset`
- `FavoriteAsset`
- `RemoveFavorite`
- `ReconcileTrackedFolders`

Each operation returns a typed result/failure, never a boolean with hidden context.

## 6. Apply transaction and crash consistency

The filesystem and SQLite cannot participate in one shared ACID transaction. Therefore Ornare uses a **durable operation journal + idempotent reconciliation**, not the false assumption that one database transaction can roll back arbitrary filesystem changes.

1. Validate target folder against `TARGET_ELIGIBILITY.md`, supported filesystem/location and actual write permissions (do not confuse the shell ReadOnly attribute with ACL write denial).
2. Resolve/capture target identity where available; reject/handle unsupported reparse/cloud placeholder states.
3. Validate source/clipboard payload.
4. Decode under image-policy limits.
5. Normalize orientation/color/alpha.
6. Render every ICO size from normalized source.
7. Write/verify canonical content-addressed asset if absent.
8. Re-read target identity and current customization immediately before mutation; abort on unsafe change.
9. Capture the **owned baseline semantics** needed for reset: prior icon file/resource/index presence/value plus relevant folder/file attributes and ownership flags. Do not treat a full historical `desktop.ini` snapshot as something that may later overwrite the live file.
10. Create a durable journal record in state `Prepared` containing operation ID, target identity, intended asset, baseline semantics and enough information to reconcile after process termination.
11. Write the new folder-owned ICO to a temp file, flush/verify, then replace/rename to its final Ornare-owned path.
12. Re-read the current `desktop.ini`; merge only the icon semantics Ornare owns while preserving all unrelated **current** values. Detect conflicting external edits to icon semantics rather than silently overwriting them.
13. Write a Unicode temp configuration and use the safest supported replacement/update path. `SHGetSetFolderCustomSettings` may be used when its documented path/API constraints are satisfied; long-path behavior is qualified separately.
14. Apply only required folder/file attributes and record whether Ornare introduced them.
15. Mark journal state `FilesystemCommitted`.
16. Notify/refresh the target through supported shell mechanisms.
17. Verify durable on-disk state and observable shell state as far as platform APIs allow. Rendering uncertainty is not equivalent to disk-state failure.
18. Commit/update SQLite assignment, history and baseline records, then mark journal `Committed`.
19. Delete obsolete Ornare-owned folder assets only after proving they are no longer referenced.
20. Remove/finalize the journal record after cleanup is complete.

Failure before step 11 leaves no user-visible folder mutation. Failure during/after commit must enter deterministic rollback or reconciliation; the next app launch must process unfinished journal entries before accepting new mutations for the same target. A correct durable folder customization is not rolled back merely because Explorer refresh confirmation is uncertain.

## 7. Dependency injection and composition

Use explicit construction or lightweight built-in DI only if it improves testability. Do not introduce a large container/service-locator architecture. Startup path must construct only services required to show the quick panel.

Lazy-load:

- Magick.NET-heavy image services until image input exists;
- SQLite/library queries not needed to render initial panel;
- manager pages until requested;
- repair/reconciliation until requested/background-idle with explicit policy.

## 8. Native AOT

Native AOT is a release optimization target, not an excuse to distort architecture. Validate it in the first vertical slice that includes WinUI + activation + Magick.NET + SQLite + packaging. If an approved dependency blocks a reliable release, create an ADR describing measured startup/size tradeoffs before changing the release mode.

## 9. Error model

Internal errors have stable domain codes, technical context, and optional native error/HRESULT. User-facing copy maps these to localized actionable messages.

Categories:

- unsupported target;
- access denied/protected location;
- invalid/unsupported image;
- image resource limit exceeded;
- storage failure;
- customization write failure;
- shell refresh uncertainty;
- package/activation failure;
- repair required.

## 10. Concurrency and activation reentrancy

Prevent simultaneous writes to the same folder customization. Coordinate operations using target identity/path locks in the app process.

The app is single-instance for the quick flow, but activations can arrive while work is in progress:

- when idle, the newest validated activation becomes the panel context;
- while only validation/preparation is running, a deliberate user action may cancel before the commit boundary;
- once filesystem commit begins, do not cancel the mutation mid-transaction;
- queue at most one pending activation while commit is in progress; coalesce duplicate requests for the same target;
- after completion/recovery, present the pending target;
- never let redirected activations mutate shared UI/domain state concurrently.

## 11. File identity

Folder path is not a perfect durable identity. Where feasible on local filesystems, store volume/file identifiers alongside path so rename/move reconciliation can improve reliability. Treat cloud provider placeholders and unavailable files cautiously.
