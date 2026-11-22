# Ornare 1.0.0 - Work Report

What was built, what was removed, and how each claim was verified. The audit findings and
their fixes live in [QA_REPORT.md](QA_REPORT.md); this document is the build record.

Repository: `https://github.com/emirtdede/Ornare`
Release: **1.0.0** (`Ornare.App`, `Ornare.Installer`, package manifest and CI all agree)
Baseline: Windows 11 25H2, x64, .NET 10, WinUI 3 on Windows App SDK

---

## 1. Feature scope

The product gained the capability set the specification asked for, implemented natively in
C# and WinUI rather than adapted from another product's source:

| Area | Delivered |
| :--- | :--- |
| Languages | 73 selectable languages, single source of truth in `LocaleCatalog`, six fully translated, the rest resolving to English |
| Setup language | The native setup offers the same 73 languages and hands the choice to the application's first run |
| Onboarding | Four separate steps: language, theme and appearance, welcome, tour. Never merged into one screen |
| Theme | System, light and dark, plus a free hex accent colour and eight swatches, with contrast-corrected foregrounds |
| Interface scale | 80, 90, 100, 110, 125, 150, 175, 200 percent, applied to the whole surface |
| App icon | Eight Ornare glyph variants, applied to the window, the taskbar, the desktop shortcut and the notification area in one action |
| Cloud backup | OneDrive, Google Drive, Dropbox or a chosen folder, with no account, no OAuth and no server |
| Diagnostics | A local report with paths anonymised, exported only when the user asks |

---

## 2. What was removed

The starting tree carried material from earlier phases that no longer matched the shipped
product. Removing it was part of the work, not an afterthought:

| Removed | Why |
| :--- | :--- |
| `.agents/rules`, `.agents/workflows` | Superseded by the contract in `AGENTS.md` and the ADRs |
| 62 files under `docs/` | Historical phase notes replaced by five current documents plus ten ADRs |
| 44 scripts under `scripts/` | One-off phase helpers; eleven remain, each referenced by CI or the README |
| `templates/`, `scratch/`, `production_artifacts/` leftovers | Stale build output and scratch material, now regenerated from source |
| Hardcoded Turkish/English ternaries in the Manager | Replaced by resource keys so all six locales work from one code path |

`docs/` now holds `ARCHITECTURE.md`, `DESIGN_SYSTEM.md`, `LOCALIZATION.md`, `QA_REPORT.md`,
`WORK_REPORT.md`, `adr/` and `screenshots/`.

---

## 3. Architecture decisions taken this release

Each of these changed behaviour or compatibility and is recorded so it is not re-litigated
by accident:

1. **The setup cannot load WinUI resources.** A native Win32 process has no `ResourceManager`
   for the application's `resw` files, so the setup carries its own 73-language table in
   `InstallerStrings.cpp`. The choice is written to
   `HKCU\Software\Ornare\InstallerLanguage` and applied by the application only when
   onboarding has not yet completed and the language is still "system".
2. **The notification-area icon is written with raw P/Invoke.** CsWin32 could not project
   `Shell_NotifyIcon` and `NOTIFYICONDATAW` for an AnyCPU assembly, so `TrayIcon.cs` uses a
   message-only window directly and re-registers on `TaskbarCreated`.
3. **The desktop shortcut icon is written with COM interop.** CsWin32 does not emit
   `CLSID_ShellLink`, so `DesktopShortcutService` declares the `IShellLinkW` and
   `IPersistFile` vtables by hand. It rewrites `IconLocation` only: Ornare never creates a
   shortcut behind the user's back and never retargets an existing one.
4. **The setup extracts its icon before pointing at it.** A shortcut that points inside
   `OrnareSetup.exe` breaks the moment the user deletes the installer, so the embedded `.ico`
   is written to `%LOCALAPPDATA%\Ornare\app-icons\installed.ico` first.
5. **Interface scale is a picker, not a slider.** Assigning `Minimum` to a `Slider` from XAML
   throws `XamlParseException` on WinUI 3; the presets live in `UserSettings.UiScalePresets`
   and the list is filled from code.
6. **Programmatic control updates are not user intent.** `ReentrancyScope` marks the window
   in which the surface rewrites its own pickers, so a refresh can never be mistaken for a
   decision. See finding 10 in the audit.

---

## 4. How the product was verified

Verification ran against the **installed** MSIX, not the build output, because package
identity, `MRT` resource lookup, the shell extension and the tray icon only exist once the
package is deployed.

### 4.1 Method

The packaged application was driven through UI Automation: real clicks on the real controls,
with the resulting state read back from the database, the registry and the filesystem rather
than from the UI's own claim.

Captures use `PrintWindow(hwnd, dc, PW_RENDERFULLCONTENT)`. That renders the window from the
DWM surface, which means the window can stay out of the way: the verification window is moved
outside the visible desktop and never raised or focused. The desktop stays usable while the
run proceeds, and the evidence is still a true rendering of the real window, not a mock.

### 4.2 Evidence

| Screenshot | Shows |
| :--- | :--- |
| `setup-language-tr.png` | Setup starting in Turkish from the Windows display language |
| `setup-language-de.png` | The same window after choosing German, every label changed |
| `setup-complete-de.png` | Completion state in German |
| `onboarding-1-language.png` | Step 1, 73 languages, native endonyms, current language preselected |
| `onboarding-2-theme.png` | Step 2, theme, accent swatches, free hex entry, scale |
| `onboarding-3-welcome.png` | Step 3, the product explanation and three concrete promises |
| `onboarding-4-tour.png` | Step 4, where things live plus the cloud toggle and resolved path |
| `manager-library.png` | The Manager after onboarding completes |
| `manager-settings.png` | Settings in Turkish, every picker in Turkish |
| `manager-settings-en.png` | The same surface in English |
| `manager-settings-about.png` | The About block, version `1.0.0`, x64, privacy statement |
| `manager-settings-icon.png` | The icon picker with the gold variant selected |
| `manager-cloud-connected.png` | The cloud card in its connected state |
| `tray-flyout.png` | Ornare present in the notification area with the chosen icon |
| `tray-area.png`, `tray-proof.png`, `desktop-full.png` | Tray registration and the shortcut on the desktop |

### 4.3 Behaviour proven by state, not by appearance

| Claim | How it was proven |
| :--- | :--- |
| The setup language reaches the application | `InstallerLanguage` = `de-DE` in the registry; onboarding step 1 opened with Deutsch selected |
| A language switch sticks | Turkish chosen, then German, then Turkish again; `Language` in the database matched the visible title, navigation and pickers at each step |
| A language switch does not disturb the icon | The shortcut `IconLocation` and the icon file timestamp were identical before and after two language switches |
| A chosen icon reaches all four surfaces | Shortcut `IconLocation` moved from `ornare-ink` to `ornare-gold`; `AppIconId` = `ornare-gold`; the tray entry still resolved to Ornare |
| Cloud backup writes real files | `ornare-cloud-sync.json` and `ornare-backup-2026-11-22.json` in `OneDrive\Ornare`, provider recorded as `OneDrive` |
| Onboarding completes | `OnboardingCompleted` became `true` after the last step |

---

## 5. Quality gates

| Gate | Result |
| :--- | :--- |
| Release build, all projects | 0 errors, 0 warnings (warnings are errors in this repository) |
| Behaviour suite (`Category!=Performance&Category!=InstalledPackage`) | 606 passed, 0 failed |
| Performance gates (`Category=Performance`) | 21 passed, 0 failed |
| Installed package (`Category=InstalledPackage`) | 1 passed, 0 failed, run on the machine with the package installed |
| CI workflow, `0e43b36` | build, behaviour, performance and static gates all green |
| `scripts/quality-gate.ps1` end to end | all automatable gates passed, exit code 0 |
| Localization parity | Enforced by test and by gate step 5: every key exists in all six complete locales |
| Dash policy | 0 em dashes, 0 en dashes, 0 horizontal bars across source, resources, docs and scripts |
| Emoji leakage | 0 in the user-facing copy the gate scans |
| Encoding integrity | Every source file is valid UTF-8 with no replacement characters |
| Version drift | `1.0.0` in `Directory.Build.props`, the package manifest and the setup resource |
| Screenshot references | Every image referenced by the documentation exists |

---

## 6. Known limits

Stated rather than hidden. None blocks the evaluation build:

1. **67 of the 73 languages are selectable but not translated.** They resolve to English. A
   language is only marked complete when its copy exists, so the picker never promises a
   translation that is missing. Translation is a content task, not a code task.
2. **ARM64 is packaged but not qualified on hardware.** No ARM64 device is available here,
   and inventing a test target is not acceptable.
3. **Windows 11 26H1 is not qualified** for the same reason.
4. **The signing certificate is a development certificate.** Public distribution needs a
   trusted code-signing certificate and Store identity.
5. **The tray icon was verified through the shell's own registry (`NotifyIconSettings`) and a
   capture of the hidden-icons flyout.** Programmatic interaction with the notification area
   is restricted by the shell, so a click-to-open-menu path is verified by hand rather than
   by automation.
