# Pre-Deployment QA and Architecture Audit

Release: **Ornare 1.0.0**
Audit date: 22 November 2026
Scope: every project in `Ornare.slnx`, plus the native shell extension and the native setup executable
Verdict: **ship-ready for the x64 evaluation build**. The two open qualification items are listed under [Known limits](#known-limits), not hidden.

This report records what was executed, what was observed, and what changed as a result. Every claim below is either reproducible by a command in the repository (test suite, CI workflow) or supported by a screenshot in [docs/screenshots](screenshots/).

---

## 1. Method

Three independent passes, in this order, and repeated until the third pass was clean:

| Pass | Question | Evidence |
| :--- | :--- | :--- |
| Behaviour | Does the product do what the requirements say, on a real installed package? | Packaged MSIX, driven through UI Automation, captured in `docs/screenshots` |
| Contracts | Do the layers agree with each other and with the specification? | 614 behaviour tests plus 13 performance gates |
| Hygiene | Are there leaks in language, characters, assets, versions or repository layout? | Repository scans documented in section 6 |

The passes were run against the **installed** application, not the build output, because package identity, resource loading, `MRT` lookup and the shell integration only exist once the package is deployed.

---

## 2. Environment

| Item | Value |
| :--- | :--- |
| OS | Windows 11 25H2, x64 |
| SDK | .NET 10.0.401 |
| UI stack | WinUI 3 / Windows App SDK (self-contained, bootstrapper disabled) |
| Package | `Ornare_1.0.0.0_x64.msix`, signed with a local development certificate |
| Setup | `OrnareSetup.exe`, native Win32, 73-language picker |
| Shell extension | `Ornare.ShellExtension.dll`, C++20 `IExplorerCommand` |
| Test runner | xUnit, `Category!=Performance` and `Category=Performance` as two runs |

---

## 3. Functional evidence

### 3.1 Setup language picker and the hand-off to onboarding

The setup executable cannot load WinUI resources, so it carries its own 73-language table in `src/Ornare.Installer/InstallerStrings.cpp`. The picker lists the same tags, in the same order, as `LocaleCatalog`.

Verified end to end on a machine reset to a clean state (package removed, `HKCU\Software\Vellium\Ornare` removed, user database removed):

1. Setup starts in Turkish because the Windows display language is Turkish. Captured as `setup-language-tr.png`.
2. German is selected from the combo box; every label on the window changes immediately. Captured as `setup-language-de.png`.
3. Install completes, the window switches to the completion state. Captured as `setup-complete-de.png`.
4. `HKCU\Software\Vellium\Ornare\InstallerLanguage` contains `de-DE`.
5. The desktop shortcut exists and points at `%LOCALAPPDATA%\Ornare\app-icons\installed.ico`.
6. The first run of the application opens onboarding with **Deutsch** selected on step 1, and the surrounding copy is German. Captured as `onboarding-1-language.png`.

This closes a real defect: before the change, the setup window was Turkish-or-English only, and the choice never reached the application, so a German user had to pick the language twice.

### 3.2 Four-step onboarding

Each step is a separate screen, never merged, and each is captured:

| Step | Screenshot | What is verified |
| :--- | :--- | :--- |
| 1. Language | `onboarding-1-language.png` | 73 languages in a scrolling list, native endonyms, current language preselected |
| 2. Theme and appearance | `onboarding-2-theme.png` | Theme radio group, accent swatches, free hex entry, interface scale |
| 3. Welcome | `onboarding-3-welcome.png` | Product explanation, three concrete promises |
| 4. Tour | `onboarding-4-tour.png` | Where things live, cloud backup toggle, and the resolved backup path |

`Start using Ornare` on the last step writes `OnboardingCompleted = true`; the subsequent window is the Manager, captured as `manager-library.png`.

### 3.3 Appearance: icon, accent, scale

The app icon picker offers eight Ornare glyph variants. Selecting one updates three surfaces in the same action:

- the window and taskbar icon through `AppWindow.SetIcon`,
- the desktop shortcut through `DesktopShortcutService`,
- the notification-area icon through `TrayIcon.UpdateIcon`.

Verified by changing the selection through UI Automation and reading the shortcut back: the `IconLocation` of `Ornare.lnk` changed from the seeded setup icon to the variant file, without the shortcut being recreated or retargeted.

`manager-settings-icon.png` records the picker in the Settings surface.

### 3.4 Cloud backup

| Check | Result |
| :--- | :--- |
| Provider detection order | `OneDriveConsumer`, `OneDrive`, `OneDriveCommercial`, then `~/OneDrive` |
| Sub-folder | `<OneDrive>\Ornare`, created when missing |
| Snapshot written | `ornare-cloud-sync.json`, 441 KB, provider recorded as `OneDrive` |
| Daily backup | `ornare-backup-2026-11-22.json` next to the snapshot |
| Atomic write | temporary `.tmp-<pid>` file plus rename, `File.Copy` fallback on `EBUSY`/`EPERM` |
| Corrupt-file chain | current, then previous, then newest backup |
| Empty-data guard | a snapshot with no folders and no settings is refused instead of overwriting a full cloud file |
| Restore prompt | offered only when local data is empty and a cloud snapshot exists |
| Automatic sync | 350 ms debounce, flushed on window close |

Connected state is captured in `manager-cloud-connected.png` and `tray-proof.png`.

### 3.5 Localization

- 73 selectable languages, single source of truth in `LocaleCatalog`.
- Six fully translated locales: `en-US`, `tr-TR`, `de-DE`, `fr-FR`, `es-ES`, `pt-BR`.
- The remaining 67 resolve to English; the picker never advertises a translation that does not exist.
- Resolution order: exact BCP-47 tag, then language-only tag, then `en-US`.
- Changing the language in Settings re-renders the window title, navigation, all section content and the tray menu without a restart.

Captured as `manager-settings.png` (Turkish) and `manager-settings-en.png` (English).

---

## 4. Test results

Two runs, because the performance gates must not compete with the behaviour suite for CPU.

### Behaviour and contracts

| Suite | Passed | Failed |
| :--- | ---: | ---: |
| `Ornare.Core.Tests` | 133 | 0 |
| `Ornare.UiTests` | 45 | 0 |
| `Ornare.Storage.Tests` | 64 | 0 |
| `Ornare.Imaging.Tests` | 212 | 0 |
| `Ornare.IntegrationTests` | 160 | 0 |
| **Total** | **614** | **0** |

### Performance gates

| Suite | Passed | Failed |
| :--- | ---: | ---: |
| `Ornare.Imaging.Tests` | 1 | 0 |
| `Ornare.Storage.Tests` | 1 | 0 |
| `Ornare.IntegrationTests` | 11 | 0 |
| **Total** | **13** | **0** |

The tenth search benchmark moved from the behaviour run into the performance run during this audit. It measures wall clock latency, so it belongs with the other timing gates and must not share a parallel run with 600 other tests.

Build: `dotnet build Ornare.slnx -c Release` completes with **0 errors and 0 warnings**. Warnings are errors in this repository (`TreatWarningsAsErrors` plus `EnforceCodeStyleInBuild`), so a warning cannot silently appear.

New suites added for this release:

| Suite | Covers |
| :--- | :--- |
| `LocaleCatalogTests` | 73 unique tags, catalogue order, exactly six complete locales, complete-first ordering, every tag resolving to itself, fallback chain, RTL flag, system locale mapping |
| `CloudSyncServiceTests` | detection and connect/disconnect, atomic write, previous file, 15-day rotation, corrupt snapshot recovery, empty-data guard, post-format restore |
| `ReentrancyScopeTests` | guarded regions are observable only while they run, nesting does not clear suppression early, a throwing region cannot leave suppression stuck on, independent scopes stay independent |
| `LocalizationParityTests` additions | runtime dictionary covers exactly the complete locales; every new key exists in all six |

---

## 5. Architecture audit

### 5.1 Layer boundaries

`Ornare.App` depends on `Core`, `Storage`, `Imaging`, `DesignSystem` and `Windows`. Nothing depends on `App`. `Core` has no UI or filesystem-write dependency. Native interop is confined to `Ornare.Windows` and the two C++ projects. No project reaches around a layer boundary, and the shell extension links only `Ornare.Windows`-free Win32 code, keeping it small and failure-isolated.

### 5.2 Invariants re-checked

| Invariant | Status |
| :--- | :--- |
| Icon writes are journaled and resumable | Holds; unchanged this release |
| Reset/Undo merge only owned icon semantics | Holds; `DesktopIniParser` still preserves foreign keys |
| Explorer is never killed or restarted | Holds; only `SHChangeNotify` is used |
| No administrator requirement | Holds; package deploys per user, setup does not request elevation |
| No account, cloud dependency, telemetry or upload | Holds; the cloud snapshot is a local file in a folder the user already syncs. No network code exists in the product |
| Single writer per SQLite store | Holds; the Manager is a single process-wide host |

### 5.3 Issues found and fixed during the audit

| # | Finding | Fix |
| :--- | :--- | :--- |
| 1 | The setup executable could not offer the application's language set | Native 73-language table, persisted to the registry, applied at first run |
| 2 | The chosen app icon did not reach the desktop shortcut | `DesktopShortcutService` rewrites `IconLocation` only, never the target |
| 3 | The chosen app icon did not reach the notification area | `TrayIcon` with a message-only window, verified against `NotifyRegistryIconSettings` |
| 4 | The desktop shortcut icon pointed at the setup executable, which is usually deleted | Setup extracts an embedded `.ico` into the per-user icon folder and points the shortcut there |
| 5 | `DestroyIconW` was declared with a `W` suffix that `user32.dll` does not export | Declared the real entry point; a dispose would otherwise have thrown |
| 6 | The icon picker items exposed no accessible name because their content is a composed panel | `AutomationProperties.SetName` on every item |
| 7 | Onboarding welcome copy claimed "no cloud dependency" after cloud backup shipped | Rewritten in all six locales to say cloud backup is optional |
| 8 | En dashes were used in ranges (`0-30%`) despite the dash policy | Normalised to `-` across sources and resources |
| 9 | The language picker's own labels were shipped as hardcoded English strings in the setup | Moved into the same native table as the rest of the setup copy |
| 10 | Changing the language in Settings reverted itself a moment later. The language handler called `ApplyLanguage`, which rebuilds the icon picker; clearing that picker raised `SelectionChanged`, and the icon handler persisted a `_settings` snapshot taken before the new language was stored, writing the old language back over the new one | `ReentrancyScope` marks programmatic picker updates, every selection handler ignores a change raised inside that scope, and a selection equal to the stored value is rejected as intent-free |
| 11 | The closed theme picker kept showing English after a language switch while its open list was translated. A closed `ComboBox` paints its selection box when the index changes, not when the selected item's `Content` is replaced | `RefreshComboBoxSelection` reassigns the index inside the re-entrancy scope for the theme, language and cloud-provider pickers |
| 12 | A user-chosen provider could reopen the folder dialog whenever the cloud surface refreshed its own selection | The provider handler returns early when provider and auto-sync state are unchanged |
| 13 | The About panel advertised the licence as **MIT** while the repository licence is proprietary, and its link targeted `LICENSE`, a file that does not exist, so it opened a 404 | `AppInfo.LicenseDisplayName` is now `Proprietary` and `LicenseUrl` points at `LICENSE.md`; the shipped build was read back through UI Automation to confirm the label |
| 14 | The acknowledgement list used a bullet character in four locales and a hyphen in two, so the same panel looked different depending on the chosen language | The bullet was replaced with `-` across `AppResources.cs` and the four affected `.resw` files; all six locales now render identically and the dash policy holds |
| 15 | `Ornare.Storage.Tests` reported one failure that could not be reproduced. Two teardown paths deleted temp files without tolerating a handle that was still open: `LibraryStorageAndScaleTests` deleted inside a bare `finally`, and `CloudSyncServiceTests` caught only `IOException`. A throw from teardown replaces the real result with an infrastructure failure | Both now delete best effort, ignoring `IOException` and `UnauthorizedAccessException`, matching the pattern the other storage suites already used. Eight consecutive suite runs before the change and three after were green |
| 16 | The first real CI run, which is the first time the suite ran on a clean machine, failed six tests that were green locally. The SVG provenance gate hashed checkout bytes, so a Windows line-ending conversion broke a hash that is a statement about the artwork; four shell-extension tests require the native DLL that `dotnet build` does not produce; and the ten-thousand-asset search benchmark ran inside the behaviour job where 600 other tests compete for the CPU | The provenance hash normalizes CRLF to LF before hashing and `.gitattributes` pins `*.svg` to LF; CI builds the shell extension with MSBuild before the test step; and the benchmark carries `Category=Performance`, warms up ten queries and samples 100 instead of 50 so P95 is not the second-worst single measurement. Verified locally: 614 behaviour tests, 13 performance gates, 0 failures |
| 17 | `scripts/quality-gate.ps1` called a deleted `spec-audit.ps1`, pointed at deleted `docs/QUALITY_GATES.md` and `templates/QUALITY_GATE_REPORT.md`, and its version step searched `AppInfo.cs` for a literal the file never contained, so the documented local gate could not pass and the CI version check would have thrown on its first run | The gate now reads the single `<Version>` in `Directory.Build.props` and compares it with `Package.appxmanifest` and `installer.rc`; the version check, dash policy, strict UTF-8, emoji, resource-parity and screenshot-reference scans are all implemented in one script that CI and a developer run identically |

---

## 6. Hygiene scans

Every scan below is implemented in `scripts/quality-gate.ps1`, which CI runs on every push. The evidence is therefore reproducible rather than a one-off manual count.

| Scan | Gate | Result |
| :--- | :--- | :--- |
| Em dash (U+2014), en dash (U+2013), horizontal bar (U+2015) | gate step 2 over `src`, `tests`, `docs`, `scripts` and the root documents | 0 in 317 files |
| Invalid UTF-8 or U+FFFD replacement character | gate step 3, strict decoding of the same file set | 0 in 317 files |
| Emoji in user-facing copy | gate step 4 over `src` XAML, `resw`, `AppResources*.cs`, `InstallerStrings.cpp` and `docs` | 0 in 38 files |
| Resource parity | gate step 5, every locale compared key for key with `en-US` | 258 keys per locale, exact match in all six |
| Version drift | gate step 1, `Directory.Build.props` vs `Package.appxmanifest` vs `installer.rc` | all `1.0.0` |
| Dead documentation links to screenshots | gate step 6 over `docs/*.md` and `README.md` | 13 references, all resolve |
| Orphan documentation | `docs/` contains only `ARCHITECTURE.md`, `DESIGN_SYSTEM.md`, `LOCALIZATION.md`, `QA_REPORT.md`, `WORK_REPORT.md`, `adr/` and `screenshots/` | clean |
| Orphan scripts | every remaining script is referenced by CI, the README or the release flow | clean |
| Stale build output and packaging leftovers | `production_artifacts/` regenerated from source, `bin`/`obj` ignored by git | clean |

Two scans that used to be manual were converted into code during this audit. `quality-gate.ps1` previously invoked `spec-audit.ps1` and referenced `docs/QUALITY_GATES.md` and `templates/QUALITY_GATE_REPORT.md`, all three of which the cleanup removed, so the documented local gate could not run at all. `perf-capture.ps1` pointed at a `docs/PERFORMANCE.md` that no longer existed. Both scripts now do what the README claims.

The emoji catalogue is deliberate product data: Ornare lets a folder carry an emoji glyph, so `EmojiCatalog.cs` is the one permitted location for emoji characters. Test fixtures may also use emoji as input data, which is why the scan targets the strings that reach a user's screen rather than every file. Four text-only glyphs (check mark, heavy check mark, ballot X, heavy ballot X) are exempt because they render as typography, not as pictographic emoji.

---

## 7. Known limits

These are stated rather than hidden, and none of them blocks the evaluation build:

1. **67 locales are selectable but not translated.** They resolve to English. A language is only marked complete when its copy exists, so the picker never promises a translation that is missing. Translation is a content task, not a code task.
2. **ARM64 is built and signed but not qualified on hardware.** There is no ARM64 device in this environment, and inventing a test target is not acceptable. The scripts produce the ARM64 package; qualification is deferred to a machine with the hardware.
3. **Windows 11 26H1 is not qualified** for the same reason: no supported hardware and build is available here.
4. **The setup certificate is a development certificate.** Public distribution requires a trusted code-signing certificate and Store identity.
5. **CI compiles the managed solution and the x64 shell extension.** The setup is a C++ project that needs the Windows 11 26100 SDK, the MSIX packaging tools and a previously built MSIX payload to embed, so CI does not build it; `scripts/create-package.ps1` and `scripts/build-multiarch-release.ps1` do, and both were run on this machine. The ARM64 builds of both native projects are likewise produced by the release scripts rather than by CI. This is a stated gap in CI coverage rather than a claim that those artifacts are covered.

---

## 8. Release checklist

| Item | State |
| :--- | :--- |
| Behaviour suite, 0 failures | done |
| Performance gates, 0 failures | done |
| Release build, 0 warnings | done |
| Installed package exercised end to end | done |
| Screenshots captured from the installed package | done |
| Localization parity enforced by tests | done |
| Cloud failure paths covered by tests | done |
| CI workflow for build, tests and quality gates | done |
| Version pinned to 1.0.0 in one place and checked against the manifest and the setup | done |
| Documentation for architecture, design, localization and QA | done |
| Trusted signing certificate | pending, external |
| ARM64 and 26H1 hardware qualification | pending, external |
