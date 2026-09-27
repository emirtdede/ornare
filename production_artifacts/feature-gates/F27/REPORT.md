# Feature Gate Report: F27 — Localization Expansion

**Feature ID:** F27  
**Feature Name:** Localization Expansion  
**Specification:** `feature-expansion/features/F27_localization-expansion.md`  
**Execution Order:** 26 of 27  
**Traceability ID:** `ORN-EXP-F27`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Completion Date:** 2026-09-27  

---

## 1. Executive Summary

Feature F27 expands Ornare's resource localization model from the original two-locale baseline (`en-US`, `tr-TR`) to six first-class locales:
1. `en-US` (English - United States, default fallback)
2. `tr-TR` (Türkçe - Turkey, first-class native)
3. `de-DE` (Deutsch - Germany)
4. `fr-FR` (Français - France)
5. `es-ES` (Español - Spain)
6. `pt-BR` (Português - Brazil)

All user-facing strings across all F01–F26 surfaces (including keyboard shortcuts, staging tray hints, placement options, background modes, templates, tags, collections, search indexing, and diagnostics) have been formalized into structured resource catalogs with 100% key and placeholder parity.

### Technical Architecture & Isolation
- **`Ornare.App` (WinUI 3):**
  - Uses `.resw` resource files under `Strings/<BCP47>/Resources.resw` (198 keys per locale).
  - Implements `AppResources.cs` as a Native AOT-compatible static dictionary manager supporting dynamic runtime language switching without restarting the process or duplicating XAML visual trees.
  - Fully dynamic switching between System Default, Türkçe, English, Deutsch, Français, Español, and Português (Brasil) in `ManagerWindow` and `QuickPanelWindow`.
- **`Ornare.ShellExtension` (Native C++ Win32 COM DLL):**
  - Maintains strict Explorer process isolation. Never loads .NET runtime, WinUI, MRT Core, Magick.NET, or SQLite into `explorer.exe`.
  - Implements native `GetLocalizedShellString(ShellStringId id)` in `dllmain.cpp` using Win32 `GetUserDefaultUILanguage()` and `PRIMARYLANGID()` matching:
    - `LANG_TURKISH` (`0x1f`) -> Turkish menu verbs ("Klasör Simgesini Ayarla", "Klasör Simgesini Sıfırla")
    - `LANG_GERMAN` (`0x07`) -> German menu verbs ("Ordnersymbol festlegen", "Ordnersymbol zurücksetzen")
    - `LANG_FRENCH` (`0x0c`) -> French menu verbs ("Définir l'icône du dossier", "Réinitialiser l'icône du dossier")
    - `LANG_SPANISH` (`0x0a`) -> Spanish menu verbs ("Establecer icono de carpeta", "Restablecer el icono de la carpeta")
    - `LANG_PORTUGUESE` (`0x16`) -> Portuguese menu verbs ("Definir ícone da pasta", "Redefinir o ícone da pasta")
    - Default fallback -> English ("Set Folder Icon", "Reset Folder Icon")
- **`Ornare.Package` (MSIX Manifest):**
  - Declares only owner-accepted languages (`en-US`, `tr-TR`) in `Package.appxmanifest` for the initial public release. The four draft locales remain preserved in source and tested for technical parity, but are unadvertised in the package manifest until professional linguistic review is completed.
- **`Ornare.Cli`:**
  - Retains English-only human CLI output and language-neutral JSON output per specification. Zero localization bloat or MRT dependencies in CLI.

---

## 2. Invariants & Gates Verification

| ID | Invariant / Requirement | Verification Method | Status |
|---|---|---|---|
| **INV-F27-01** | Six app locales with 100% key parity (198 keys each) | Automated test `AllSixLocalesHaveMatchingKeysAndNonEmptyValues` in `LocalizationParityTests` | **PASS** |
| **INV-F27-02** | Zero empty values, zero replacement characters / mojibake (`\uFFFD`, `Ã`) | Automated string scan in `LocalizationParityTests` across all 6 `.resw` files | **PASS** |
| **INV-F27-03** | Format placeholder parity (`{0}`, `{1}`) across all locales | Automated regex placeholder validation in `LocalizationParityTests` | **PASS** |
| **INV-F27-04** | AOT-safe in-memory `AppResources` key parity | Automated test `AppResourcesHasMatchingKeysForAllSixLocales` verifies parity against `en-US` | **PASS** |
| **INV-F27-05** | Native Shell Extension isolation (no managed code in Explorer) | Source audit of `src/Ornare.ShellExtension/dllmain.cpp` using Win32 `PRIMARYLANGID` | **PASS** |
| **INV-F27-06** | Package manifest public locale policy | Manifest restricts `<Resource Language="..."/>` to owner-accepted `en-US` and `tr-TR` | **PASS** |
| **INV-F27-07** | Turkish language typography & diacritics | `TurkishTypographyContainsSpecificDiacritics` verifies ç, ğ, ı, ö, ş, ü, İ preservation | **PASS** |
| **INV-F27-08** | Multi-architecture compilation | Clean compilation for `win-x64` and `win-arm64` Release with 0 warnings and 0 errors | **PASS** |

---

## 3. Linguistic Quality Status

Per section 5 and 6 of `feature-expansion/features/F27_localization-expansion.md`, technical resource certification is strictly separated from professional native linguistic review. New draft locales must be explicitly marked:

| Locale | Language | Technical Resource Status | Linguistic Certification Status | Notes |
|---|---|---|---|---|
| `en-US` | English (United States) | **PASS** (198 keys) | **OWNER_ACCEPTED** | Canonical reference locale |
| `tr-TR` | Türkçe (Turkey) | **PASS** (198 keys) | **OWNER_ACCEPTED** | First-class native product locale |
| `de-DE` | Deutsch (Germany) | **PASS** (198 keys) | **FUNCTIONAL_DRAFT_NOT_LINGUISTICALLY_CERTIFIED** | Technical resource PASS; Microsoft glossary aligned; pending human native review |
| `fr-FR` | Français (France) | **PASS** (198 keys) | **FUNCTIONAL_DRAFT_NOT_LINGUISTICALLY_CERTIFIED** | Technical resource PASS; Microsoft glossary aligned; pending human native review |
| `es-ES` | Español (Spain) | **PASS** (198 keys) | **FUNCTIONAL_DRAFT_NOT_LINGUISTICALLY_CERTIFIED** | Technical resource PASS; Microsoft glossary aligned; pending human native review |
| `pt-BR` | Português (Brasil) | **PASS** (198 keys) | **FUNCTIONAL_DRAFT_NOT_LINGUISTICALLY_CERTIFIED** | Technical resource PASS; Microsoft glossary aligned; pending human native review |

---

## 4. Test Evidence

### Automated Test Suites
- **`Ornare.UiTests`**: 16 tests passed (including `AllSixLocalesHaveMatchingKeysAndNonEmptyValues`, `TurkishTypographyContainsSpecificDiacritics`, `EnglishAndTurkishResourcesHaveMatchingKeys`, and `AppResourcesHasMatchingKeysForAllSixLocales`).
- **`Ornare.Core.Tests`**: 64 tests passed.
- **`Ornare.Imaging.Tests`**: 178 tests passed.
- **`Ornare.Storage.Tests`**: 54 tests passed.
- **`Ornare.IntegrationTests`**: 120 tests passed.
- **Total Test Count:** **432 passed**, **0 failed**, **0 skipped**.

### Multi-Architecture Builds
- **Release win-x64:** Build succeeded with 0 warnings, 0 errors.
- **Release win-arm64:** Build succeeded with 0 warnings, 0 errors.

---

## 5. Gate Signoff

Feature F27 (Localization Expansion) meets all technical requirements of `feature-expansion/features/F27_localization-expansion.md`, `AGENTS.md`, and `docs/QUALITY_GATES.md`.

**Technical Gate Status:** **PASS**  
**Linguistic Gate Status:** **PASS (en-US, tr-TR) / FUNCTIONAL_DRAFT_NOT_LINGUISTICALLY_CERTIFIED (de-DE, fr-FR, es-ES, pt-BR)**
