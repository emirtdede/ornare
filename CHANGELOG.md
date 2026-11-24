# Changelog

All notable changes to Ornare will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Fixed
- **Explorer ignores valid `desktop.ini` after the June 2026 security updates**: a file carrying a Mark of the Web is no longer reported as applied. `ShellTrustGuard` clears the `Zone.Identifier` stream from Ornare-owned files, and a folder that still carries one is reported as `ConfigMismatch` so the manager offers Repair instead of a false success.
- **Long child paths failed on a valid folder**: `desktop.ini` and the owned ICO are now committed with extended-length (`\\?\`) paths, so a 250-character folder no longer fails with `ERROR_PATH_NOT_FOUND` for a 262-character `desktop.ini`. The folder-path limit itself is unchanged and now documented honestly (ADR-0011).
- **Two atomic writers**: `desktop.ini` and export commits share one `AtomicFileWriter`, so write-through, extended paths and `ReplaceFileW` fallback are implemented once instead of twice.
- **Crash-safe `desktop.ini` writes**: apply, reset and undo now write through a sibling temp file and an atomic write-through rename, so a crash can never leave a truncated or zero-byte `desktop.ini`.
- **Legacy encoding preservation**: `desktop.ini` is read and written back in its original encoding (UTF-16 LE/BE, UTF-8 with or without BOM, or the system ANSI code page), so Turkish and other legacy files are no longer corrupted with replacement characters.
- **Half-applied folders are repairable**: a `NeedsRepair` journal entry is reconciled at startup (rolled back only when the folder no longer shows an Ornare icon) instead of staying stuck forever.
- **Lost activations are reported**: a redirected launch that cannot reach the UI thread now tells the user instead of failing silently.
- **Atomic settings save**: a settings change is written in one transaction, so a crash cannot leave a partial set such as a new language with the old theme.
- **No UI freeze on exit**: closing a window flushes the debounced cloud snapshot without blocking the UI thread.
- **Targeted Explorer refresh**: a folder icon change invalidates only that folder's cache; the global association-changed notification (which froze every open Explorer window) is gone.
- **Diagnostics never shown as user copy**: raw exception text, paths and HRESULTs are logged as diagnostic detail; users see localized messages.
- **Guarded event handlers**: an unexpected failure in an `async void` handler is logged and shown once as a localized dialog instead of escaping to the dispatcher.
- **Restart-independent first frame**: the persisted language and theme are applied before the first window exists, so the UI no longer flashes English/system defaults. The quick panel also honours the chosen theme and starts in the chosen language instead of the OS language.
- **Correct caption colour on the first frame**: the title bar buttons are coloured from the requested theme, which is known before the visual tree loads.
- **Release gate script actually runs**: `scripts/quality-gate.ps1` no longer aborts after the version check, so every automatable gate (version, dash policy, encoding, emoji, resource parity, screenshots, Release build, behaviour tests, performance gates) is exercised again. `scripts/capture-runtime-ui.ps1` enables visual test mode for its child process so the reference screenshots keep matching the requested theme and language.
- **Relative cloud snapshots**: cloud snapshots record a redacted folder label and fingerprint instead of absolute user paths.
- **`desktop.ini` read-modify-write race**: the merge is re-validated against the live file and retried when another program changed it.
- **Accessible names were English in all six locales**: the parity test accepted any assignment on an element, so `AccentHexBox.Text = ...` hid `AutomationProperties.Name="Accent color hex"`. The test now matches the exact property. Six names were fixed: both accent-hex fields, the interface-scale picker, the cloud toggle, and both "Remove Emblem" buttons.
- **Every COM entry point in the shell extension is crash-safe**: `GetTitle`, `GetIcon`, `GetToolTip`, `GetState`, `Invoke` and `DllGetClassObject` now catch every exception, and the project compiles with `/EHa`. An escaping `std::bad_alloc` from `CoTaskMemAlloc` used to call `std::terminate` inside `explorer.exe`; now a fault degrades one menu item instead of terminating the shell (INV-SHELL-001).
- **Image staging is single-flight**: two concurrent drag-drops, pastes or picker selections used to interleave into one staged ICO. Staging now takes an atomic `_stagingGate` and refuses a second operation until the first releases it in `finally` (INV-CON-005).
- **Input images are bounded before they are read**: a 2 GB file no longer reaches `ReadAllBytesAsync`. The panel checks `FileInfo.Length` and the clipboard stream size against the shared `OrnareConstants.MaxInputImageBytes` (64 MB) before allocating and shows localized `ErrorImageTooLarge` copy (INV-CON-006).
- **The newest preview wins**: preview renders used to finish out of order, so a slow render could paint over a newer one and leave an arbitrary card's preview on screen. Each render now carries a sequence number and a debounced, cancellable hover path; a superseded render discards its own result and a closed panel touches no visual (INV-CON-007).
- **A closed panel stops writing to its visual tree**: `Dispose` sets a `_disposed` flag first, releases the language subscription and cancels in-flight preview work; `GuardAsync` skips the error surface once the window is disposed.
- **The theme/language error copy cannot be confused with a diagnostic**: `ShowCategorizedError` now takes a `QuickPanelErrorCopy` value, so a raw exception message can no longer compile into the rendered slot (INV-UI-005).
- **An open panel follows a language change**: the manager raised a language change only for itself, leaving an open quick panel rendering a mixture of two languages. `AppResources.CurrentLocaleChanged` now notifies live windows, which repaint their chrome and drop the subscription on close (INV-UI-006).
- **Numbers and dates follow the chosen language**: window copy formats through `AppResources.CultureFor(language)` instead of `CultureInfo.CurrentCulture`, so a count is formatted in the same culture as its sentence on every screen.
- **The accent hex field validates as you type**: an invalid value disables Apply and is announced through the field's help text, instead of failing only after a click.
- **Guarded fire-and-forget startup work**: cloud-restore prompt, collections load and cloud-surface refresh now run through a guard that logs a failure instead of leaving an unobserved task.

### Added
- `docs/COMPETITOR_COMPARISON.md`: eleven products scored out of 100 against nine weighted criteria, with the evidence behind every score and a two-part verdict. Three Microsoft Store entrants (Folderly, IconStyler, Fold11) were added because folder-icon competition moved there; the verdict names Folderly, not an old desktop tool, as the closest challenger.
- Regression tests for `desktop.ini` encoding round-trips, `NeedsRepair` reconciliation, settings persistence, cloud path redaction, the no-global-Shell-invalidation contract, and runtime localization key/placeholder parity.
- Architecture guards that fail if a second `desktop.ini` writer, a direct write in `FolderCustomizer`, or a missing trust check is reintroduced, plus `LongPath` unit tests covering extended, UNC, device-namespace and relative inputs.
- Mandatory engineering documents: `LOGIC_INVARIANTS`, `QUALITY_GATES`, `REQUIREMENTS_TRACEABILITY`, `DEFINITION_OF_READY_DONE`, `LONG_PATH_COMPATIBILITY`, `LOGGING_DIAGNOSTICS`, `PERFORMANCE`, `OSS_REUSE`, `SECURE_SDLC` and `THREAT_MODEL`.

### Changed
- `QuickPanelWindow` is split into partial files: `QuickPanelWindow.Placement.cs` (Win32 interop, chrome, monitor maths), `QuickPanelWindow.TempAssets.cs` (every temporary PNG's life), `QuickPanelWindow.Catalogs.cs` (emblem and emoji catalogs), `QuickPanelWindow.Localization.cs` (the copy list and language subscription), `QuickPanelWindow.Commit.cs` (apply, undo, reset) and `QuickPanelWindow.Errors.cs` (processing state and error surfaces). The window class dropped from 3,238 to about 2,220 lines and each concern is reviewable on its own.
- `SqliteStorageService` is split into a query file and `SqliteStorageService.Schema.cs` (schema, migrations V2-V6, connection opening, foreign-key checks), so a migration review no longer scrolls through 4,200 lines of query code.
- The Manager's language surface moved to `ManagerWindow.Localization.cs`; the window class is about behaviour again. Its tag management and custom-collection flows moved to `ManagerWindow.Library.cs` and `ManagerWindow.Collections.cs`, so `ManagerWindow.xaml.cs` dropped from about 2,900 to about 2,150 lines.
- The shell extension's five `IExplorerCommand` implementations now share `OrnareCommandBase`. The file went from 1,632 to 1,131 lines with one `Invoke` and two `ShellExecuteExW` call sites instead of five and eight.
- `docs/ARCHITECTURE.md` now describes the code as built (single write funnel, trust guard, path handling, storage layout) instead of proposing future projects.
- `docs/WORK_REPORT.md` was removed: its evidence and gate tables duplicated `docs/QA_REPORT.md`, and its six unique release decisions are preserved in ADR-0012.
- `docs/PRE_DEPLOY_AUDIT_2026-11-22.md` was removed for the same reason: a dated snapshot of a tree invites a reader to treat it as current state. ADR-0013 records where each class of finding now lives (changelog, invariants, ADRs, tests) and lists the debts the report used to carry.
- The CLI JSON envelope's C# property is now `Message` (the wire name `messageKey` is unchanged, so schema 1 stays frozen).
- Startup and activation breadcrumbs are retained only during an explicit diagnostic session, keeping the 256-entry diagnostic ring buffer for real failures.

---

## [1.0.0-rc.1] - 2026-09-25

### Added
- **Windows 11 Modern Context Menu Integration**: Native C++ Win32 `IExplorerCommand` shell extension (`Ornare.ShellExtension.dll`) registering "Set Folder Icon" / "Klasör Simgesini Ayarla" on directories in Windows 11 File Explorer.
- **Quick Panel Surface**: Minimalist, card-free WinUI 3 quick panel supporting drag-and-drop, file picker, clipboard paste, Recent icons strip, Favorites strip, and instantaneous Undo / Reset actions.
- **High-Fidelity Imaging Pipeline**: Secure Magick.NET-based image decoder generating true multi-resolution 10-frame Windows `.ico` files (16, 20, 24, 32, 40, 48, 64, 96, 128, 256 px) directly from normalized source pixels with Lanczos filtering.
- **Durable Persistence Engine**: Folder-owned customization architecture storing icons locally in `.ornare\folder.ico` with relative path references, ensuring folder mobility across paths and volumes without broken links.
- **Desktop.ini Semantic Merge**: Safe merger preserving third-party sections (`[ViewState]`, `InfoTip`, localized resource names, comments) during apply and reset operations.
- **Transactional Journal & Crash Recovery**: SQLite-backed write-ahead transaction journal with automatic, idempotent startup reconciliation for interrupted operations.
- **Conflict & Health Detection**: Active scanning identifying orphaned icons, configuration mismatches, missing folder attributes, and external customizations without destructive overwrites.
- **Manager Application**: Unified desktop interface featuring Library grid with real-time search, Recent history, Favorites management, Customized Folders list with one-click repair, Settings with live theme and language toggle, and Privacy-safe Diagnostic Export.
- **First-Class Bilingual Localization**: Complete parity for English (`en-US`) and Turkish (`tr-TR`) across all UI surfaces, strings, and shell verbs.
- **Comprehensive Accessibility**: Full keyboard navigation, logical tab ordering, High Contrast theme adaptation, and UI Automation accessible names on all interactive controls.
- **MSIX Packaging**: Self-contained x64 MSIX package targeting Windows 11 (build 26100+), compliant with Windows App Certification Kit (WACK) standards.

### Security & Privacy
- **100% Offline & Local**: Zero network capabilities declared in AppxManifest, zero network API dependencies, zero outbound telemetry, zero analytics, zero external logging.
- **Standard User Execution**: Requires no administrative elevation, no UAC prompts, and no dangerous privilege requirements.
- **Strict Image Security Policy**: Explicit format allowlist (PNG, JPEG, WebP, BMP, GIF, TIFF, ICO), memory/disk resource caps (256 MB memory / 512 MB disk), decompression bomb rejection, and exclusion of untrusted SVG script execution.
- **Path Anonymization**: Diagnostic export automatically redacts user identifying profile paths in diagnostic logs.

### Known Limitations
- **ARM64 Physical Execution**: Managed code compiles cleanly for ARM64; however, physical ARM64 hardware qualification is NOT PROVEN in this test environment.
- **Physical Reboot Persistence**: Post-reboot folder icon caching is logically guaranteed by Windows Explorer relative `.ini` architecture, but physical OS reboot validation remains NOT PROVEN within the continuous agent environment.
- **Drive Support**: Fully verified on local NTFS volumes; cloud-only placeholder folders and remote network/WebDAV shares are unsupported in V1.
- **Commercial Release Gate**: Signed with development certificate (`CN=Ornare`); commercial Microsoft Store distribution requires production trusted signing and trademark clearance.

---

## [0.1.0] - 2026-09-24

### Initial Baseline
- Established architecture invariants, threat model, specification baseline, and initial engineering structure.
