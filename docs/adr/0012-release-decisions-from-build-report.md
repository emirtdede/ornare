# ADR-0012: Six release decisions that were only recorded in a build report

- Status: Accepted
- Date: 2026-11-24

## Context

`docs/WORK_REPORT.md` was the last remaining build-record document. Most of it duplicated
`docs/QA_REPORT.md` (evidence tables, quality gates, known limits), which is exactly the kind of
duplication that lets two documents disagree with each other. It also held six decisions that
appear nowhere else. Deleting the report without preserving them would have lost the reasoning;
keeping the report would have kept the duplicate claims.

## Decision

The six decisions are promoted here, and `docs/WORK_REPORT.md` is removed.

1. **The setup cannot load WinUI resources.** A native Win32 process has no `ResourceManager` for
   the application's `.resw` files, so the setup carries its own table in `InstallerStrings.cpp`.
   The choice is written to `HKCU\Software\Ornare\InstallerLanguage` and applied by the app only
   while onboarding has not completed and the language is still "system". The registry key path is
   duplicated in `OrnareConstants.ProductRegistryKeyPath` and `kProductRegistryKeyPath` and must be
   kept in sync by hand.
2. **The notification-area icon uses raw P/Invoke.** CsWin32 does not project `Shell_NotifyIcon`
   and `NOTIFYICONDATAW` for this assembly shape, so `TrayIcon.cs` owns a message-only window and
   re-registers on `TaskbarCreated`.
3. **The desktop shortcut icon uses hand-declared COM interop.** CsWin32 does not emit
   `CLSID_ShellLink`, so `DesktopShortcutService` declares the `IShellLinkW` and `IPersistFile`
   vtables directly. It rewrites `IconLocation` only: Ornare never creates a shortcut behind the
   user's back and never retargets an existing one.
4. **The setup extracts its icon before pointing at it.** A shortcut that points inside
   `OrnareSetup.exe` breaks when the installer is deleted, so the embedded `.ico` is written to
   `%LOCALAPPDATA%\Ornare\app-icons\installed.ico` first.
5. **Interface scale is a picker, not a slider.** Assigning `Minimum` to a `Slider` from XAML throws
   `XamlParseException` on WinUI 3, so the presets live in `UserSettings.UiScalePresets` and the
   list is filled from code.
6. **Programmatic control updates are not user intent.** `ReentrancyScope` marks the window in
   which a surface rewrites its own pickers, so a refresh can never be mistaken for a user choice.

## Consequences

- One less document that can drift, and six reasons that now live where the other architectural
  reasons live.
- The verification evidence and known limits remain only in `docs/QA_REPORT.md`, so there is a
  single place to correct when a number changes.

## Alternatives considered

- **Keep the report and mark its numbers as historical.** Rejected: two documents stating
  different test counts is the confusion the cleanup was meant to remove.
- **Move all six into individual ADRs.** Rejected as noise; they are release decisions of one
  release, not six separate architectural pivots.
