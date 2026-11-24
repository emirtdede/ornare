# Software Architecture Specification (as built)

This document describes the architecture that exists in the repository, not an intention. Where
the code and this document disagree, the code is wrong or the document is wrong and one of them
must be fixed in the same change. The previous version of this file described "proposed"
projects in the future tense while the product already shipped; that gap is what let the long-path
promise in `LONG_PATH_COMPATIBILITY.md` drift away from the code (see ADR-0011).

## 1. Architecture goal

Keep Explorer integration tiny and fail-safe; keep the application modular enough to test every
rule without launching WinUI, but not so abstract that a desktop utility becomes a framework.

## 2. Process boundaries

### Explorer process

`Ornare.ShellExtension.dll` is the only Ornare code loaded inside Explorer. It is C++ with no
managed runtime, and it does exactly four things:

1. validate that exactly one local folder is selected;
2. expose title, icon, state and (for Reset) a tooltip;
3. build a minimal activation payload;
4. return immediately.

Forbidden inside Explorer: WinUI, Magick.NET, SQLite, network access, image decoding, ICO
generation, repair scans, heavy logging and synchronous disk traversal.

The five `IExplorerCommand` implementations (set icon, apply last, undo, reset, flyout root) share
one base class, `OrnareCommandBase`, which owns reference counting, `QueryInterface`, the
`CoTaskMemAlloc` title buffer and the two-step "packaged protocol first, unpackaged CLI second"
launch. Before this, each command carried its own copy of that logic, including its own copy of
the launch sequence; a defect fixed in one copy would have survived in the others, and Explorer
crashes when this DLL faults. There is now one `Invoke` and two `ShellExecuteExW` call sites.

Because this DLL faults inside `explorer.exe`, every COM entry point - `GetTitle`, `GetIcon`,
`GetToolTip`, `GetState`, `Invoke` and `DllGetClassObject` - is wrapped in `catch (...)`, and the
project compiles with `/EHa` so a structured exception is caught rather than unwinding through
Explorer's frames (INV-SHELL-001). An escaping `std::bad_alloc` would otherwise call
`std::terminate` and take the shell down with it.

### App process

`Ornare.App` owns UI, validation, imaging, folder customization, the database, history, repair,
diagnostics and settings.

## 3. Projects

| Project | Responsibility |
|---------|----------------|
| `Ornare.App` | Packaged WinUI 3 application: activation, quick panel, manager, onboarding |
| `Ornare.DesignSystem` | Tokens and reusable WinUI controls |
| `Ornare.Core` | Domain contracts, validation, result/error models, path helpers |
| `Ornare.Imaging` | Secure decode, normalize, render, ICO/PNG export |
| `Ornare.Storage` | SQLite metadata, schema and migrations, library, tags, collections, search |
| `Ornare.Windows` | Folder customization, `desktop.ini` parsing and writing, shell refresh, trust checks |
| `Ornare.ShellExtension` | Native C++ COM implementation |
| `Ornare.Package` | MSIX manifest, assets, package registration |
| `Ornare.Cli` | Grammar, handlers and the JSON envelope |

## 4. The single write funnel

Every `desktop.ini` mutation, from any code path, goes through
`Ornare.Windows.DesktopIniFileWriter`. This is the most important structural rule in the product
because it is where three invariants meet, and a caller cannot accidentally get one without the
others:

1. **Atomic commit.** Content is written to a sibling temp file, flushed with
   `Flush(flushToDisk: true)`, then committed with `MoveFileExW(MOVEFILE_REPLACE_EXISTING |
   MOVEFILE_WRITE_THROUGH)`. A crash cannot leave a truncated or zero-byte file (INV-FS-002).
2. **Encoding preservation.** `DesktopIniParser` reads the file with its original encoding
   (UTF-16 LE, UTF-16 BE, UTF-8 with or without BOM, or legacy ANSI via the active code page) and
   `EncodeForWrite` writes it back the same way. A character the legacy code page cannot represent
   promotes the file to UTF-8 with a BOM instead of corrupting the user's text (INV-FS-003).
3. **Trusted origin.** The file Ornare just wrote cannot legitimately carry a Mark of the Web, so
   `ShellTrustGuard` removes any inherited `Zone.Identifier` stream (INV-FS-004).

Before this funnel existed there were two independent atomic writers (one in `FolderCustomizer`,
one in `AtomicExportWriter`'s sibling logic) and neither cleared the Mark of the Web.

Optimistic concurrency is part of the funnel: the merge re-reads the file after the read and
before the commit, and retries up to three times, so a concurrent writer is detected rather than
overwritten.

## 5. Why Windows may ignore a correct file, and what the product does about it

The June 2026 security updates (KB5094126 on Windows 11 24H2 and 25H2) make Explorer ignore a
`desktop.ini` whose origin it cannot verify, and can refuse to extract an icon from a file carrying
a Mark of the Web. The visible effect is that a folder keeps its default icon while the file on
disk is correct, which is exactly the shape of a false success.

`Ornare.Windows.ShellTrustGuard` classifies the risk (`FolderPresentationRisk`) and clears the
marker for Ornare-owned files. `FolderCustomizer.GetFolderState` reports such a folder as
`ConfigMismatch` rather than `Healthy`, so the manager offers Repair instead of claiming the icon
is applied. This is the only competitor-distinguishing capability the product has, and it is a
property of the write funnel rather than a feature bolted on top.

## 6. Path handling

- The customizable folder path is bounded at 259 characters; longer input is rejected with a
  localized message. The reasoning and the alternative that was rejected are in ADR-0011.
- Every child path (`desktop.ini`, `.ornare\folder.ico`, the temp file) is extended with `\\?\`
  by `Ornare.Core.LongPath.ToExtended` before it reaches a Win32 function. A 250-character folder
  produces a 262-character `desktop.ini` path, which would otherwise fail with
  `ERROR_PATH_NOT_FOUND`.
- The app manifest declares `longPathAware`, so .NET's own `System.IO` handling is already correct.

## 7. Activation

The shell extension activates `ornare://<verb>?v=1&path=<url-encoded>`. If packaged activation
fails, it falls back to `Ornare.App.exe --<verb> "<path>"`. The payload contains no state beyond
the verb, the path and an optional cursor hint; the app revalidates the path and filesystem state
after activation. Single-instance redirection means a second activation while work is in progress
becomes a queued context change, never a concurrent mutation.

## 8. Apply transaction and crash consistency

The filesystem and SQLite cannot share an ACID transaction, so Ornare uses a durable operation
journal plus idempotent reconciliation. The implemented order is:

1. validate the target (`TargetFolderValidator`): path form, local drive, not a volume root, not a
   reparse point, not a cloud placeholder, not under Windows or Program Files;
2. capture the owned baseline semantics needed for Reset (prior icon resource/index, whether
   Ornare introduced the ReadOnly attribute);
3. stage the ICO inside the folder (`.ornare\folder.ico`), keeping the previous one for Undo;
4. merge only Ornare's icon semantics into the live `desktop.ini` through the funnel, preserving
   every unrelated current value and detecting external edits to the icon keys;
5. apply the folder attributes the shell requires, recording whether Ornare introduced them;
6. emit `FilesystemCommitted`, then `ShellRefreshRequested` after a targeted `SHCNE_UPDATEITEM`;
7. let the calling service write assignment, library asset, history and NTFS identity, then emit
   `Completed`.

`FolderCustomizer` deliberately does **not** emit `Completed`. If it did, a crash between the
filesystem commit and the metadata write would look like a rollback candidate on the next launch
while the icon was already on disk (INV-FS-006). `ReconcileStartupAsync` processes unfinished
journal entries before new mutations for the same target are accepted, and
`LiveFolderStillOwnedByOrnare` re-reads the live file (raw bytes, ASCII or UTF-16) so a rollback
never discards a customization that is actually still in place.

## 9. Storage layout

`SqliteStorageService` is a partial class split by responsibility:

- `SqliteStorageService.cs` - queries: assignments, assets, history, settings, render cache, tags,
  collections, search;
- `SqliteStorageService.Schema.cs` - initial schema, forward migrations V2-V6, connection opening,
  foreign-key verification and the guards that decide whether the database may be touched.

Splitting them means a migration review reads one file instead of 4,200 lines of query code, and
the migration tests point at a stable target. Migrations read `PRAGMA table_info` and add only
missing columns, and every identifier that reaches dynamically built SQL passes
`IsSafeSqlIdentifier`.

## 10. Error model

`OperationResult` carries three distinct things and they must never be mixed:

- `ErrorCode`: a stable domain category, safe to branch on;
- `ErrorMessage`: user-facing copy, never containing a path, SQL identifier, HRESULT or exception
  text (INV-UI-004);
- `DiagnosticDetail`: the raw exception or Win32 detail, written to the diagnostic log and never
  rendered.

The UI renders only localized copy selected by `ErrorCode` or by an explicit caller-supplied
message. A `try/catch` that swallows an exception and reports success is a defect, not a
simplification.

## 11. Localization

Six first-class locales (en-US, tr-TR, de-DE, fr-FR, es-ES, pt-BR). `AppResources` holds the
runtime dictionaries and is the only source of copy. There is no `.resw` mirror: the old
`Strings/*/Resources.resw` tree was incomplete (259 of 436 keys), was never compiled into a
`resources.pri`, and was compared only against itself, so it was deleted under audit finding 3.4.
Windows do not flash English: every window applies theme and language after
`InitializeComponent` and before it is activated.

The XAML still carries English design-time literals; the runtime overwrites every one of them. That
is only acceptable because the parity test now checks the *exact property*: a `Text` assignment no
longer excuses an unlocalized `PlaceholderText` or `AutomationProperties.Name`. The stricter rule
exposed six accessible names that were English in all six locales (both accent-hex fields, the
scale picker, the cloud toggle and both "Remove Emblem" buttons) and they are now localized.
Moving the literals to `x:Uid` remains tracked debt (ADR-0013), not a correctness bug: with the
strict guard, an unlocalized literal cannot ship unnoticed.

### Window composition

The two large windows are partial classes split by responsibility, so behaviour reads without
scrolling past interop or tables:

| File | Contains |
| :--- | :--- |
| `QuickPanelWindow.xaml.cs` | interaction flow: pickers, staging gate, preview scheduling |
| `QuickPanelWindow.Commit.cs` | apply, undo and reset: the only place the panel mutates the filesystem |
| `QuickPanelWindow.Errors.cs` | processing-state transitions and every localized error surface |
| `QuickPanelWindow.Placement.cs` | Win32 interop, window chrome, monitor placement maths |
| `QuickPanelWindow.TempAssets.cs` | creation, bounding and deletion of every temporary PNG |
| `QuickPanelWindow.Catalogs.cs` | emblem and emoji catalogs: load, filter, select, stage |
| `QuickPanelWindow.Localization.cs` | the flat localized-copy list and the language-change subscription |
| `ManagerWindow.xaml.cs` | manager behaviour |
| `ManagerWindow.Settings.cs` | settings surface behaviour |
| `ManagerWindow.Library.cs` | library tag management |
| `ManagerWindow.Collections.cs` | custom collections: list, create, rename, reorder, delete, membership |
| `ManagerWindow.Localization.cs` | manager chrome copy |

A live preview is a pure function of the staged state, so it is cached by
(template, version, colour, emblem) and every render carries a monotonic sequence number. The
newest request wins; a superseded render discards its own result, and a render that finishes
after the panel closed touches no visual (INV-CON-007). Hover previews are debounced through a
cancellable delay, so sweeping across the template grid starts one raster, not one per card.

## 12. Concurrency

- One `SemaphoreSlim` guards database access per service instance.
- `CloudSyncService` uses a process-wide write gate, and snapshot paths are redacted (hashed leaf
  plus a short fingerprint) so a diagnostic export never leaks a user's folder names.
- `desktop.ini` merges are protected by the funnel's read-commit-recheck loop.
- UI handlers never run `async void` without a guard; `GuardAsync` converts an escaped exception
  into a localized error surface instead of a process termination.
- Known gap: `WaitAsync(cancellationToken)` is not yet propagated through every storage call, so a
  cancelled apply can finish its query. It is a responsiveness gap, not a corruption risk, because
  the journal and the merge are already cancellation-independent.

## 13. Dependency direction

`App -> (Cli, Windows, Storage, Imaging, DesignSystem) -> Core`. `Core` depends on nothing but the
base class library. `Windows` never references `App`. No project references `ShellExtension`; it
communicates only through activation.

## 14. Native AOT and packaging

Native AOT remains a release optimization target (ADR-0009, ADR-0010), not an architectural
constraint. The x64 evaluation package is built by `scripts/create-package.ps1`; ARM64 and Windows
11 26H1 are produced but not qualified on hardware, and that limit is stated in
`docs/QA_REPORT.md` rather than hidden.
