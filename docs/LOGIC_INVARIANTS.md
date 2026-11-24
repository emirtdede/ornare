# Ornare - Logic Invariants

This document is the canonical registry of the behaviour rules that Ornare treats as
non-negotiable. Code comments cite the identifiers below (`INV-*`); a change that would
violate one requires an accepted ADR and the invariant must be updated in the same change.

Invariants are grouped by domain:

- `INV-TX` - transaction, journal and reconciliation.
- `INV-FS` - filesystem mutation and ownership.
- `INV-CON` - concurrency and process lifetime.
- `INV-DATA` - user data, storage and integrity.
- `INV-UI` - the user interface contract.
- `INV-SHELL` - the Explorer extension contract.

## INV-TX - Transactions and reconciliation

### INV-TX-001 - Journal states are written before they are depended on
Every mutating operation records a journal row in `Prepared` before any filesystem change,
advances it through `AssetGenerated` -> `FilesystemStaged` -> `FilesystemCommitted` ->
`ShellRefreshRequested` -> `MetadataCommitted` -> `Completed`, and never skips a stage that a
later stage assumes. Verification: `tests/Ornare.IntegrationTests/JournalStageContractTests.cs`.

### INV-TX-002 - Reconciliation is idempotent and never destroys committed state
Startup reconciliation may be run any number of times. Non-committed states
(`Prepared`, `AssetGenerated`, `FilesystemStaged`) roll back; committed states
(`FilesystemCommitted`, `ShellRefreshRequested`, `MetadataCommitted`) complete. A
`NeedsRepair` entry is only rolled back when the live folder no longer shows an Ornare icon;
otherwise it stays pending so the user can run Repair. Verification:
`tests/Ornare.IntegrationTests/FaultInjectionCrashRecoveryTests.cs`,
`SqliteStorageServiceTests.cs`.

## INV-FS - Filesystem mutation

### INV-FS-001 - Only Ornare-owned icon semantics are ever rewritten
Reset and Undo merge the baseline prior semantics into the live `desktop.ini`. They never
overwrite unrelated sections, keys or a newer full snapshot produced by another tool.

### INV-FS-002 - `desktop.ini` writes are atomic and encoding-preserving
A write goes to a sibling temp file, is flushed to disk, then renamed with
`MoveFileExW(MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH)`. The original encoding
(UTF-16 LE/BE, UTF-8 with BOM, or system ANSI) is preserved. A partially written
`desktop.ini` is never observable.

### INV-FS-003 - The `.ornare` container is the only file store Ornare removes
Reset/Undo delete `folder.ico`, `folder.prev.ico` and the `.ornare` directory **only when it
is empty**. No user file outside `.ornare` and `desktop.ini` is deleted.

### INV-FS-004 - Customization is independent of the source image
After apply, deleting, moving or renaming the original source image leaves the folder icon
intact, because the canonical ICO is content-addressed and owned by Ornare. Verification:
`tests/Ornare.IntegrationTests/FolderCustomizationIntegrationTests.cs`.

### INV-FS-005 - External icon conflicts are never silently overwritten
When the live `desktop.ini` points to an icon Ornare did not write, Reset/Repair return
`ErrorCode.ExternalConflict` unless the caller explicitly forces resolution.

### INV-FS-006 - "Applied but not recorded" is a repairable state, never a silent success
If the filesystem commit succeeds but metadata/journal rows cannot be written, the journal
row becomes `NeedsRepair` and the UI reports `ErrorAppliedNeedsRepair`. Reporting failure
would invite a retry that re-applies an already-applied icon.

### INV-FS-007 - One write funnel, and the result is trusted by Explorer
Every `desktop.ini` mutation goes through `DesktopIniFileWriter`, so no code path can write
atomically without also preserving the encoding and clearing an inherited Mark of the Web.
A file carrying a `Zone.Identifier` stream is ignored by Explorer after the June 2026 security
updates, so `FolderCustomizer.GetFolderState` reports such a folder as `ConfigMismatch` instead
of `Healthy`; claiming success while the shell renders the default icon is a false success.
Ornare-owned ICO files are normalized the same way before the shell is asked to render them.
Verification: `tests/Ornare.IntegrationTests/DesktopIniEncodingTests.cs`.

## INV-CON - Concurrency and process lifetime

### INV-CON-001 - One operation per target folder
All mutating operations on a folder acquire `PerTargetSyncLock`. A second operation waits up
to five seconds and then fails with `ErrorCode.FolderLocked`. Verification:
`tests/Ornare.IntegrationTests/OperationConcurrencyIntegrationTests.cs`.

### INV-CON-002 - UI state transitions follow the durable state
The panel may show progress before a commit, but the final success/error state is derived
from the durable result, not from an optimistic guess.

### INV-CON-003 - Exactly one manager window per process
All manager entry points route through `ManagerWindowHost`. Two manager windows mutating the
same SQLite store are a defect.

### INV-CON-004 - Explorer is never killed or restarted
No product flow terminates or restarts `explorer.exe`. Cache invalidation is limited to
targeted `SHChangeNotify` calls for the affected folder and its owned ICO.

### INV-CON-005 - Image staging is single-flight through an atomic gate
`QuickPanelWindow` admits one image staging operation at a time through an `Interlocked`
counter (`_stagingGate`), never through derived UI state. A second drag-drop, paste or picker
selection while staging is in flight is refused instead of racing the first. The gate is
released in `finally`, so a failed read cannot wedge the panel. Verification:
`tests/Ornare.UiTests/QuickPanelRuntimeGuardsTests.cs`.

### INV-CON-006 - Input images are bounded before they are read
No path reads an arbitrary user file into memory. `StageImageAsync` checks `FileInfo.Length`
and the clipboard path checks the stream size against `OrnareConstants.MaxInputImageBytes`
before allocating, and an oversized payload is rejected with localized copy. A bound applied
after `ReadAllBytesAsync` is already too late to prevent the allocation.

### INV-CON-007 - The newest preview wins
Live preview renders take a monotonic sequence number. A render whose sequence has been
superseded, or that completes after the panel closed, discards its own result rather than
painting over a newer one. Hover previews are debounced and cancel the previous request.
Verification: `tests/Ornare.UiTests/QuickPanelRuntimeGuardsTests.cs`.

## INV-UI - User interface contract

### INV-UI-004 - The user never sees backend failure text
A service message (`ErrorMessage` from a service layer, an exception message, an HRESULT or a
schema name) is diagnostic material. The UI renders localized copy from `AppResources` and
records the service message through `App.Log` / `DiagnosticDetail`. Verification: guarded
event handlers, `ShowServiceError` in `QuickPanelWindow`, and
`tests/Ornare.UiTests/LocalizationParityTests.cs`.

### INV-UI-005 - The error copy channel is type-separated
`ShowCategorizedError` takes a `QuickPanelErrorCopy`, not two `string?` parameters. The user
message and the diagnostic detail are distinct static factories (`Message` / `Diagnostic`), so
passing an exception message into the rendered slot does not compile. Verification:
`tests/Ornare.UiTests/QuickPanelRuntimeGuardsTests.cs`.

### INV-UI-006 - An open window follows a language change
A window that outlives a language change made elsewhere subscribes to
`AppResources.CurrentLocaleChanged`, aliases the new locale, repaints its chrome and releases
the subscription on close. A closed window never observes the event. Verification:
`tests/Ornare.UiTests/QuickPanelRuntimeGuardsTests.cs`.

### INV-UI-007 - One culture decides number and date formatting
Copy formatting inside a window uses `AppResources.CultureFor(language)`, derived from the
chosen UI language, not `CultureInfo.CurrentCulture`. A count, date or percentage therefore
matches the language the surrounding text is written in.

## INV-SHELL - Explorer extension contract

### INV-SHELL-001 - No exception may escape a COM entry point
Every `IExplorerCommand` method and `DllGetClassObject` is wrapped in `catch (...)`. `Invoke`
runs on Explorer's UI thread inside a `noexcept` boundary, where an escaping `std::bad_alloc`
or `std::length_error` calls `std::terminate` and takes down `explorer.exe`. The project also
compiles with `/EHa` (`<ExceptionHandling>Async</ExceptionHandling>`) so a hardware and
structured exception is caught rather than propagated. Verification:
`tests/Ornare.UiTests/QuickPanelRuntimeGuardsTests.cs`.

## INV-DATA - Storage and user data

### INV-DATA-001 - Settings changes are all-or-nothing
`SaveUserSettingsAsync` writes every setting inside one transaction, so a crash cannot leave
a half-applied set (for example a new language with the old theme).

### INV-DATA-002 - Reset/Undo never overwrite a newer full `desktop.ini`
See INV-FS-001. A newer snapshot written by another tool wins over a stale baseline.

### INV-DATA-003 - Cloud snapshots cannot leak full local paths
Snapshots written to a synced folder record a redacted folder label and a stable
fingerprint, never the raw absolute path.

### INV-DATA-004 - Diagnostics never carry user-facing copy
`OperationResult.DiagnosticDetail` carries technical detail and is only written to logs or a
CLI's stderr. `OperationResult.ErrorMessage` is diagnostic text produced by a service layer;
the UI never renders it (INV-UI-004) and instead resolves localized copy from `AppResources`.
