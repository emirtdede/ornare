# Ornare - Microsoft Store Release Checklist

Use this checklist for a release distributed through the Microsoft Store: identity reserved in
Partner Center, submission uploaded as an MSIX, certification passed and rollout staged. It is
not the GitHub checklist; the two channels differ in identity, signing, update control and the
legal surface that applies.

How to use it: run the commands, tick a row only when the evidence exists, and record the
evidence next to it. A row without evidence is not done. Rows marked **blocking** stop the
submission; advisory rows are recorded with a decision.

The Store build and the GitHub build must never share a package identity. If they did, the
Store would treat the GitHub build as a foreign package and both channels would fight over the
same install location, settings and update path.

---

## 0. Preconditions

| # | Check | How to verify | Blocking |
| :--- | :--- | :--- | :--- |
| 0.1 | The GitHub checklist sections 1 and 2 are green for the same commit | `docs/RELEASE_CHECKLIST_GITHUB.md` | yes |
| 0.2 | The Store identity is reserved and its `Package/Identity/Name`, `Publisher` and `PublisherDisplayName` are copied from Partner Center, not invented | compare `Package.appxmanifest` with the Partner Center identity page | yes |
| 0.3 | The package family name used by the app for its data root belongs to the Store identity, not the GitHub identity | install the Store build and read `Get-AppxPackage Ornare`, then confirm `OrnareConstants.PackageFamilyName` | yes |
| 0.4 | The version is higher than the last submitted version | Partner Center submission history | yes |
| 0.5 | The application title shown to users is the reserved name, unchanged | manifest `DisplayName` | yes |

---

## 1. Store-specific package requirements

| # | Check | How to verify | Blocking |
| :--- | :--- | :--- | :--- |
| 1.1 | The MSIX is built from a clean tree with the Store identity, not the GitHub identity | `pwsh ./scripts/create-package.ps1` with the Store manifest | yes |
| 1.2 | Target device family is Windows 11 desktop; the minimum version matches the 25H2 baseline | manifest `TargetDeviceFamily` | yes |
| 1.3 | Architectures submitted are x64 and ARM64, each built and tested | `pwsh ./scripts/build-multiarch-release.ps1` | yes |
| 1.4 | The package installs per user and needs no administrator prompt | clean VM, standard user | yes |
| 1.5 | No unsupported extension, service, driver or startup task is declared | manifest capability review | yes |
| 1.6 | The app declares only the capabilities it uses | manifest review against `docs/THREAT_MODEL.md` | yes |
| 1.7 | Application icons are supplied at every Store-required scale, rasterised from the SVG masters, with no transparent-edge leak at 400 percent | inspect the produced `Assets/` set | yes |
| 1.8 | StoreLogo and every tile and splash asset render correctly on light and dark | manual, on a device | yes |
| 1.9 | No development certificate is embedded; Store signing replaces it | `signtool verify` on the uploaded file after Store signing | yes |
| 1.10 | The MSIX passes the Windows App Certification Kit | `appcert.exe` in WACK, full mode | yes |
| 1.11 | Install, update, repair and uninstall each succeed on a clean VM and on an upgraded VM | manual | yes |
| 1.12 | Uninstall leaves applied folder icons and the user's data root intact | manual, then inspect | yes |

---

## 2. Store listing

| # | Check | How to verify | Blocking |
| :--- | :--- | :--- | :--- |
| 2.1 | Description, short description and feature list are written for an end user, with no internal identifiers, no emoji and no marketing superlatives | read the drafted listing | yes |
| 2.2 | No em dash, en dash, horizontal bar or typographic arrow appears in any listing field | paste every field through a scan | yes |
| 2.3 | Every screenshot is from the Store build, current, and free of placeholder text or a development watermark | visual review | yes |
| 2.4 | Screenshots show the real product in at least two languages, including Turkish | visual review | yes |
| 2.5 | The category and subcategory describe the product accurately (personalisation, not system utility that changes shell behaviour destructively) | read the selection | yes |
| 2.6 | Age rating questionnaire answers are true and produce the expected rating | IARC questionnaire | yes |
| 2.7 | Privacy policy URL is live and states plainly that there is no account, no telemetry and no upload | open it | yes |
| 2.8 | Support URL and website URL resolve | open both | yes |
| 2.9 | Copyright and publisher name match `LICENSE.md` | read both | yes |
| 2.10 | The listing does not claim a feature the build does not have, including theme count, languages actually translated, or AI features | compare with `docs/RELEASE_CHECKLIST_GITHUB.md` section 10 | yes |
| 2.11 | "What's new" text is written for this version only | read it | advisory |

---

## 3. Certification notes and reviewer instructions

| # | Check | How to verify | Blocking |
| :--- | :--- | :--- | :--- |
| 3.1 | Notes explain that the product adds a context-menu command and does not restart Explorer | read the notes | yes |
| 3.2 | Notes state that no account, credential or network access is needed to test the main flow | read the notes | yes |
| 3.3 | Notes name a test folder the reviewer can right-click and a one-line expected result | read the notes | yes |
| 3.4 | Notes mention that the optional backup writes only to a folder the user picks | read the notes | yes |
| 3.5 | Notes disclose the shell extension and that it is out-of-process and fails closed | read the notes | yes |
| 3.6 | Any previous certification failure is addressed explicitly, with the change that fixes it | read the notes | yes |

---

## 4. Store policies that commonly block a folder tool

| # | Check | How to verify | Blocking |
| :--- | :--- | :--- | :--- |
| 4.1 | The app does not modify files outside the folders the user chose | code review of every write path | yes |
| 4.2 | The app does not change shell behaviour globally or disable Explorer features | review `ShellRefreshService` and the extension | yes |
| 4.3 | The app has no advertising, no upsell interstitial and no third-party analytics | code review | yes |
| 4.4 | The app does not download executable code at runtime | review the update path | yes |
| 4.5 | The app is fully functional offline; the cloud backup folder is optional and local by default | disconnect the network and run the main flow | yes |
| 4.6 | Accessibility claims in the listing match the build (keyboard, Narrator, high contrast) | repeat `docs/RELEASE_CHECKLIST_GITHUB.md` section 5 against the Store build | yes |
| 4.7 | The app handles a missing or read-only chosen folder with an explanation, not a crash | manual | yes |

---

## 5. Store update path

| # | Check | How to verify | Blocking |
| :--- | :--- | :--- | :--- |
| 5.1 | Updates are delivered by the Store; the app's own updater is disabled in the Store build | settings and code review | yes |
| 5.2 | An in-app "check for updates" in the Store build points at the Store, not the GitHub feed | manual | yes |
| 5.3 | Updating keeps settings, assignments, history and the cloud snapshot link | update N to N+1 on a device with data | yes |
| 5.4 | Version numbering is monotonic and never reused after a failed submission | Partner Center history | yes |
| 5.5 | The identity never changes after the first published version | compare with the first submission | yes |

---

## 6. Rollout

| # | Check | How to verify | Blocking |
| :--- | :--- | :--- | :--- |
| 6.1 | A flight (package flight group) is published before the public rollout | Partner Center | advisory |
| 6.2 | Flight installs are re-verified for first-run language, theme, apply and reset | manual | yes |
| 6.3 | Crash and hang rates in Partner Center health are reviewed after the first 24 hours | Partner Center health report | yes |
| 6.4 | Rollout is paused automatically if the health report shows a regression against the previous version | documented decision and owner | yes |
| 6.5 | The public rollout percentage is stated in the release record | release record | advisory |

---

## 7. Sign-off

| Role | Name | Date | Verdict |
| :--- | :--- | :--- | :--- |
| Engineering |  |  |  |
| QA |  |  |  |
| Security |  |  |  |
| Product owner |  |  |  |
| Store submission owner |  |  |  |

A submission may go to certification only when every blocking row is ticked with evidence and
every sign-off row is filled.

---

## 8. Open items that block this channel today

Verified against the tree, not assumed. Each one must be closed or explicitly deferred in
writing before the first Store submission.

| Item | State | Note |
| :--- | :--- | :--- |
| Store identity reserved in Partner Center | open | the current manifest carries the GitHub identity, so a Store submission cannot be built yet |
| Data-root family name follows the submitted identity | open | `OrnareConstants.PackageFamilyName` is a constant; it must match whichever identity the build ships under, and a channel switch must not orphan a user's database |
| Store-signed package verified on a device | open | not possible without a reserved identity |
| ARM64 qualification on real hardware | open | no supported device in this environment |
| Windows 11 26H1 qualification | open | no supported build or hardware in this environment |
| Listing assets and copy | open | no Store listing exists yet |
| Privacy policy page | open | `PRIVACY.md` is written and accurate; a public URL is not published yet |
| Store build disables the GitHub updater | open | the update path currently assumes the GitHub feed |
| Feature parity with the release checklist scope | open | same gaps as `docs/RELEASE_CHECKLIST_GITHUB.md` section 10; the Store listing must not promise them |
