# ADR-0015: Harden the UI and shell boundaries found by the November 2026 audit

- Status: Accepted
- Date: 2026-11-23

## Context

A second pre-deployment review of the built product (not a preview tree) found defects that
compile cleanly and pass the existing suite, yet can terminate Explorer or corrupt a user's
staged work:

1. The native shell extension protected no COM entry point. `Invoke` runs on Explorer's UI
   thread inside `noexcept`; an escaping `std::bad_alloc` from `CoTaskMemAlloc` calls
   `std::terminate` and takes `explorer.exe` down. This is the worst class of defect in the
   product: the extension is the only Ornare code in the shell process.
2. Image staging had no real reentrancy lock. The guard keyed on `_isProcessing`, which is a UI
   flag set *after* work starts, so two drag-drops, pastes or picker selections could interleave
   into a single staged ICO. Separately, no input size bound existed: a multi-gigabyte file
   reached `ReadAllBytesAsync` and could exhaust memory.
3. Live preview renders completed out of order. A slow render could paint over a newer one, so
   hovering across the template grid could leave an arbitrary card's preview on screen. The
   window also kept writing to its visual tree after `Dispose`.
4. `ShowCategorizedError` took two `string?` parameters, so an exception message passed into the
   rendered slot compiled and showed paths and schema names to the user (a violation of the
   existing INV-UI-004 that the type system did not prevent).
5. An open quick panel did not observe a language change made in the manager; its copy mixed two
   languages until the next activation. Window copy also formatted numbers with
   `CultureInfo.CurrentCulture` while the text was in the chosen language.

## Decision

- **Shell extension**: wrap every COM entry point (`GetTitle`, `GetIcon`, `GetToolTip`,
  `GetState`, `Invoke`, `DllGetClassObject`) in `catch (...)` and compile with `/EHa`
  (`<ExceptionHandling>Async</ExceptionHandling>`). A fault degrades one menu item; it never
  reaches Explorer. Recorded as INV-SHELL-001.
- **Staging**: admit one staging operation through an `Interlocked` gate (`_stagingGate`),
  released in `finally`, never through derived UI state. Recorded as INV-CON-005.
- **Input bound**: reject an oversized payload before allocation, checking `FileInfo.Length` and
  the clipboard stream size against `OrnareConstants.MaxInputImageBytes` (64 MB). Recorded as
  INV-CON-006.
- **Preview**: give every render a monotonic sequence number, cache the pure render by
  (template, version, colour, emblem), debounce hover through a cancellable delay, and discard a
  superseded or post-close result. Recorded as INV-CON-007.
- **Error copy**: replace the two-string signature with a `QuickPanelErrorCopy` value split into
  `Message` (rendered) and `Diagnostic` (logged). Recorded as INV-UI-005.
- **Language**: raise `AppResources.CurrentLocaleChanged` from the locale setter; a live window
  aliases the locale, repaints and unsubscribes on close. Format numbers and dates through
  `AppResources.CultureFor(language)`. Recorded as INV-UI-006 and INV-UI-007.

## Consequences

- A defect in the shell extension can no longer take the user's desktop with it.
- The staged state has one writer at a time and a bounded memory cost per attempt.
- The visible preview always reflects the newest request.
- INV-UI-004 is enforced by the compiler, not only by review.
- A language change reaches every open window without a restart.
- `QuickPanelWindow` is further split (`Commit.cs`, `Errors.cs`), so the commit path and the
  error surfaces are reviewable without reading the picker handlers.

## Verification

- `tests/Ornare.UiTests/QuickPanelRuntimeGuardsTests.cs` fails if any of these protections is
  removed: the shell catch-all and `/EHa`, the staging gate and its `finally`, the pre-read
  bound, the preview sequence check, the error-copy factory split, and the language subscription
  and release.
- The shell extension is rebuilt in Release x64 so the `/EHa` flag is exercised.
- Full solution build is 0 warnings / 0 errors and the behaviour suite passes.

## Alternatives considered

- **Only log a shell fault.** Rejected: a log line cannot run when the process is terminating.
- **Use `_isProcessing` as the staging lock.** Rejected: it is set after the work begins, so the
  second operation sees `false` and proceeds. A test would pass while the race remained.
- **Bound the image after reading it.** Rejected: the allocation is the failure.
- **Serialize previews with a mutex.** Rejected: it would queue renders instead of superseding
  them, so the paint would still be the last-started, not the last-requested.
- **Keep two `string?` parameters and rely on review.** Rejected: the type is the gate.
