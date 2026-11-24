# Ornare - GitHub Release Checklist

Use this checklist for a release published from `github.com/emirtdede/ornare`: a signed MSIX
attached to a GitHub Release, with the source tagged and the CI workflow green. It is not the
Store checklist; the two channels differ in identity, signing, update mechanism and legal
surface, so they are signed off separately.

How to use it: run the commands, tick the row only when the stated evidence exists, and record
the evidence next to the row. A row without evidence is not done, no matter how obvious the
answer looks. Rows marked **blocking** stop the release; rows marked advisory are recorded and
may ship.

---

## 0. Preconditions

| # | Check | How to verify | Blocking |
| :--- | :--- | :--- | :--- |
| 0.1 | Repository has exactly two long-lived branches, `main` and `development` | `git branch -a` lists no third local or remote branch | yes |
| 0.2 | `main` contains only commits that passed CI | `gh run list --branch main --limit 5` shows every run green | yes |
| 0.3 | Release commit is on `main` and tagged | `git describe --tags --exact-match` prints `v1.0.0` | yes |
| 0.4 | Working tree is clean | `git status --porcelain` prints nothing | yes |
| 0.5 | Version is stated once and matches everywhere | `pwsh ./scripts/quality-gate.ps1 -SkipBuild -SkipTests` (step 1) | yes |

---

## 1. Repository hygiene

| # | Check | How to verify | Blocking |
| :--- | :--- | :--- | :--- |
| 1.1 | No em dash (U+2014), en dash (U+2013) or horizontal bar (U+2015) in any text surface | quality gate step 2 | yes |
| 1.2 | Every file is valid UTF-8 with no U+FFFD replacement character | quality gate step 3 | yes |
| 1.3 | No file carries a UTF-8 byte order mark (`.editorconfig` declares `utf-8`) | quality gate step 3 | yes |
| 1.4 | No emoji in shipped copy; the emoji catalogue is the only permitted location | quality gate step 4 | yes |
| 1.5 | All six complete locales hold the same key set | quality gate step 5 reports 436 keys for each | yes |
| 1.6 | No second copy of the runtime strings exists (`src/Ornare.App/Strings` stays deleted) | quality gate step 5 fails if the tree returns | yes |
| 1.7 | Every screenshot referenced by a document exists | quality gate step 6 | yes |
| 1.8 | `docs/` holds only current documents; no dated audit snapshot is presented as state | `docs/` listing reviewed against `docs/QUALITY_GATES.md` | advisory |
| 1.9 | No secret, key, token or certificate password is committed | `git grep -nIE "(password|secret|api[_-]?key|token)\s*="` reviewed; `.gitignore` covers `.env` | yes |
| 1.10 | Build output, packages and user data are ignored | `git status --ignored --porcelain` shows `bin`, `obj`, `production_artifacts` ignored | yes |
| 1.11 | Project root is organised into `src`, `tests`, `docs`, `scripts`, `assets` with no loose working files | top-level listing reviewed | advisory |

---

## 2. Build and tests

| # | Check | How to verify | Blocking |
| :--- | :--- | :--- | :--- |
| 2.1 | Debug build has 0 warnings and 0 errors (warnings are errors here) | `dotnet build Ornare.slnx -c Debug` | yes |
| 2.2 | Release build has 0 warnings and 0 errors | `dotnet build Ornare.slnx -c Release` | yes |
| 2.3 | The native shell extension builds for x64 | MSBuild on `src/Ornare.ShellExtension/Ornare.ShellExtension.vcxproj` | yes |
| 2.4 | Every test assembly passes | `dotnet test Ornare.slnx -c Release --filter "Category!=Performance&Category!=InstalledPackage"` | yes |
| 2.5 | Every performance gate passes on a quiet machine | `dotnet test Ornare.slnx -c Release -m:1 --filter "Category=Performance"` | yes |
| 2.6 | The one test that needs an installed package passes | `pwsh ./scripts/quality-gate.ps1 -InstalledPackageOnly` | yes |
| 2.7 | Dependency audit is clean | `pwsh ./scripts/dependency-audit.ps1` | yes |
| 2.8 | CI is green on the release commit | `gh run list --commit $(git rev-parse HEAD)` | yes |
| 2.9 | Test counts in `docs/QA_REPORT.md` match the run | compare the printed totals with the table | advisory |

---

## 3. Packaging and identity

| # | Check | How to verify | Blocking |
| :--- | :--- | :--- | :--- |
| 3.1 | MSIX builds for x64 | `pwsh ./scripts/create-package.ps1` | yes |
| 3.2 | MSIX builds for ARM64 | `pwsh ./scripts/build-multiarch-release.ps1` | yes |
| 3.3 | `Package.appxmanifest` `Identity Name`, `Publisher` and `PackageFamilyName` agree with the installed package | install, then compare with `Get-AppxPackage Ornare` | yes |
| 3.4 | `DisplayName`, `Description` and publisher name are end-user language, not internal identifiers | manifest review | yes |
| 3.5 | Every declared `<Resource Language>` entry resolves; onboarding offers the same 73 tags | `docs/LOCALIZATION.md` plus a manual picker check | yes |
| 3.6 | App icons are SVG masters, rasterised at every required scale, with no quality loss or transparent-edge leak | inspect `assets/` and the produced `Package.appxmanifest` scale assets at 100, 125, 150, 200, 400 percent | yes |
| 3.7 | No icon resolution or format is missing from the package payload | `makeappx unpack` and list `Assets/` | yes |
| 3.8 | Package is signed with a certificate the recipient can trust | `signtool verify /pa /v <msix>` | yes |
| 3.9 | The certificate in `production_artifacts/release` is the production certificate, not the development one | certificate subject and thumbprint reviewed | yes |
| 3.10 | Install, repair, update and uninstall all work per user without an administrator prompt | manual run of all four on a clean VM | yes |
| 3.11 | Uninstall leaves user data and already-applied folder icons intact | manual run, then check the data root and a customised folder | yes |

---

## 4. Product behaviour

Run this section against the **installed** package, not the build output, and capture a
screenshot for anything the row claims.

| # | Check | How to verify | Blocking |
| :--- | :--- | :--- | :--- |
| 4.1 | First run opens onboarding in the language chosen during setup, not the OS language | install in Turkish on an English machine and the reverse | yes |
| 4.2 | The wizard shows no text or control under the caption buttons at any DPI | screenshot at 100 and 200 percent | yes |
| 4.3 | Every onboarding step has a visible way forward and back; no dead end exists | manual walkthrough | yes |
| 4.4 | Opening the Quick Panel on an untouched folder stages no colour and writes nothing until Apply | apply-inspect: the folder's `desktop.ini` is unchanged after open and close | yes |
| 4.5 | Apply writes an icon, Explorer refreshes only that folder, and no other window is disturbed | manual, with two Explorer windows open | yes |
| 4.6 | Reset removes only Ornare-owned icon keys and preserves foreign `desktop.ini` values | pre-seed a foreign key, reset, re-read the file | yes |
| 4.7 | Undo restores the previous icon without overwriting a newer external snapshot | manual, with an external edit in between | yes |
| 4.8 | An interrupted apply is reconciled on the next launch, not left half-written | kill the process during apply, relaunch, inspect | yes |
| 4.9 | The Manager's empty library offers a way to choose a folder, not only Import | delete all assignments and open the library | yes |
| 4.10 | The Quick Panel explains itself on first use and the hint stays dismissed afterwards | reset `QuickPanelHintDismissed`, open, apply, reopen | yes |
| 4.11 | Every empty state (library, recent, folders, favourites) carries a guidance line, not a blank pane | manual | yes |
| 4.12 | No popup, warning or error breaks the layout at any scale from 80 to 200 percent | manual sweep | yes |
| 4.13 | No operation leaves the UI looking frozen: every wait longer than 200 ms has visible progress | manual, forced slow path via a large image | yes |
| 4.14 | Transitions are smooth and none animates a layout property that reflows content | visual review plus `docs/DESIGN_SYSTEM.md` | advisory |
| 4.15 | The close button behaviour (exit or keep in tray) is a setting and is honoured on next launch | toggle, close, check the tray and Task Manager | yes |
| 4.16 | The tray icon opens the Manager and its menu can exit the app | manual | yes |
| 4.17 | Windows native notifications appear for a completed background operation, and the in-app surface shows the same state | manual, with notifications enabled and disabled | yes |
| 4.18 | Diagnostics export writes a report with personal paths anonymised and no crash text shown as user copy | export and read the file | yes |
| 4.19 | Every link in the app and in the documents opens the intended destination | click each link in Settings and About | yes |
| 4.20 | Every keyboard shortcut is documented and works, including the shell menu accelerators | `docs/DESIGN_SYSTEM.md` plus manual run | yes |
| 4.21 | Settings are grouped into categories a user can predict, with no orphan toggle | manual review | yes |
| 4.22 | Theme choice (system, light, dark), accent colour including custom hex, and interface scale all apply immediately and survive a restart | manual | yes |
| 4.23 | The CLI and the app share one data root, so a CLI apply is visible, undoable and repairable in the UI | apply from `Ornare.Cli.exe`, open the Manager | yes |
| 4.24 | `--help`, `--version` and `inspect` answer within the documented budget, and a redirected stderr receives no interactive notice | `docs/PERFORMANCE.md`, `CliStartupContractTests` | advisory |

---

## 5. Accessibility and localisation

| # | Check | How to verify | Blocking |
| :--- | :--- | :--- | :--- |
| 5.1 | Every interactive control has an accessible name in the current language | Narrator sweep in Turkish and English | yes |
| 5.2 | Every essential action is reachable by keyboard alone, with a visible focus ring | keyboard-only run | yes |
| 5.3 | High contrast and 200 percent text scaling clip nothing | manual | yes |
| 5.4 | No untranslated English literal appears in a Turkish, German, French, Spanish or Portuguese screen | manual sweep of every screen | yes |
| 5.5 | Dates, times and numbers follow the chosen language, not the OS culture | change language, compare a timestamp | yes |
| 5.6 | The 67 untranslated locales fall back to English and the picker does not promise otherwise | select an untranslated locale | advisory |
| 5.7 | Turkish casing and length do not break a button, header or tooltip | review `Sıfırla`, `Görünüm`, long German compounds | yes |

---

## 6. Legal and documentation set

| # | Check | How to verify | Blocking |
| :--- | :--- | :--- | :--- |
| 6.1 | `LICENSE.md` matches the intended distribution model | owner sign-off | yes |
| 6.2 | `THIRD-PARTY-NOTICES.md` covers every dependency actually shipped, with its licence text | `pwsh ./scripts/dependency-audit.ps1` plus a manual diff against the package payload | yes |
| 6.3 | `SECURITY.md` states a reachable disclosure route and a response expectation | read it | yes |
| 6.4 | `CONTRIBUTING.md` describes branch, commit and review rules that match reality | read it | advisory |
| 6.5 | `PRIVACY.md` states plainly that there is no account, no telemetry and no upload | read it, then confirm against the code | yes |
| 6.6 | `CHANGELOG.md` has an entry for this version written for an end user | read it | yes |
| 6.7 | `README.md` installs, builds and uninstalls as written, and its screenshots are current | follow it from a clean clone | yes |
| 6.8 | In-app documentation (About, diagnostics help, troubleshooting text) is current for this version | read every surface | yes |
| 6.9 | Support and sponsor links point at live destinations (`https://github.com/emirtdede/ornare`, `https://github.com/sponsors/emirtdede`) | click both | yes |

---

## 7. GitHub release mechanics

| # | Check | How to verify | Blocking |
| :--- | :--- | :--- | :--- |
| 7.1 | Tag `v1.0.0` is annotated and pushes to `main` | `git tag -v v1.0.0`, `git push origin v1.0.0` | yes |
| 7.2 | Release notes are user-facing and free of internal identifiers | read the drafted body | yes |
| 7.3 | Assets are the signed x64 and ARM64 MSIX plus a checksum file | `gh release view v1.0.0` | yes |
| 7.4 | Checksums match the uploaded bytes | `sha256sum -c` on the downloaded assets | yes |
| 7.5 | The release is not marked latest until the x64 install has been re-verified from the downloaded asset | install from the release asset on a clean VM | yes |
| 7.6 | The repository description, topics and social preview carry no emoji and no internal jargon | repository settings | advisory |
| 7.7 | Dependabot alerts are triaged; no open critical advisory | `gh api repos/emirtdede/ornare/dependabot/alerts` | yes |
| 7.8 | Issue and pull-request templates exist and match the workflow actually used | read `.github/` | advisory |

---

## 8. Update path

| # | Check | How to verify | Blocking |
| :--- | :--- | :--- | :--- |
| 8.1 | The update check compares against the GitHub release feed, not the Store identity | read `docs/ARCHITECTURE.md` update section | yes |
| 8.2 | An update over an installed build keeps settings, assignments and history | install N, then N+1, inspect | yes |
| 8.3 | A downgrade or a corrupted download is refused with an explanation | manual | yes |
| 8.4 | Manual "check for updates" has a visible result, including "already current" | manual | yes |

---

## 9. Sign-off

| Role | Name | Date | Verdict |
| :--- | :--- | :--- | :--- |
| Engineering |  |  |  |
| QA |  |  |  |
| Security |  |  |  |
| Product owner |  |  |  |

A release may be published only when every blocking row is ticked with evidence attached and
every sign-off row is filled. Advisory rows are recorded with a decision, not skipped silently.

---

## 10. Open items that block this channel today

These are verified against the tree, not assumed, and each one must be closed or explicitly
deferred in writing by the product owner before a public GitHub release.

| Item | State | Note |
| :--- | :--- | :--- |
| Production code-signing certificate | open | only the development certificate exists in `production_artifacts/release` |
| ARM64 and Windows 11 26H1 qualification | open | no supported hardware in this environment |
| Update feed channel separation | open | the Store and GitHub channels must not overwrite each other's identity |
| Fifteen themes plus custom hex | open | the product ships system, light and dark plus an accent colour; the wider theme set is not implemented |
| Optional app PIN (4, 6 or 8 digits) | open | not implemented |
| Close button behaviour setting | open | the tray exists; the user-facing choice does not |
| MCP server, terminal commands and webhooks | open | the CLI exists; MCP and webhooks do not |
| Optional local AI tiers and RAG | open | not implemented |
| winget package | open | no manifest published |
| Ready-made setting presets | open | status presets exist; appearance presets do not |
| Backup, restore, recovery, delete, clean start and reset-settings-only | partial | backup, restore, recovery and reset exist; a one-month soft delete with a second confirmation for permanent delete is not implemented |
| Per-action keyboard shortcut customisation | open | shortcuts are fixed, not user-assignable |
