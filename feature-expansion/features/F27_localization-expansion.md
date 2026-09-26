# F27 — Localization Expansion

**Status:** IMPLEMENT WITH EXTERNAL LINGUISTIC-QUALITY GATE  
**Dependencies:** all production strings stable after F26  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Expand Ornare's existing en-US/tr-TR app resource model to de-DE, fr-FR, es-ES and pt-BR without hard-coded production strings, WinUI/managed loading inside Explorer, or false claims of professional linguistic review.

## Fixed decisions

1. App UI supports exactly six locales after this feature: `en-US`, `tr-TR`, `de-DE`, `fr-FR`, `es-ES`, `pt-BR`; English is fallback and Turkish remains first-class.
2. `Ornare.App` uses `.resw`/MRT Core as before. Qualification builds declare all six languages for technical testing; the **public release manifest declares en-US/tr-TR plus only new locales whose linguistic status is `OWNER_ACCEPTED` or `PRO_REVIEWED`**.
3. `Ornare.Cli` human output remains **English-only in this expansion**; JSON is language-neutral. Do not make CLI depend on MRT/WinUI merely for localization.
4. `Ornare.ShellExtension` remains native/tiny. Its menu strings use native Win32 resources. Extend the existing native resource mechanism; if it is not already MUI-capable, use Win32 MUI language-specific resource files (`Ornare.ShellExtension.dll.mui`) / STRINGTABLE resources rather than loading managed/WinUI/MRT code in Explorer.
5. New de/fr/es/pt-BR resource values may be drafted by the coding agent using authoritative Microsoft terminology/glossaries and context, but this only certifies **resource completeness and functional rendering**.
6. Public claim `professionally localized/native-reviewed` for a new language is **NOT PROVEN** until either the product owner explicitly accepts that locale's translations or evidence of native/professional linguistic review is provided. **Public release packaging must expose only locales whose status is `OWNER_ACCEPTED` or `PRO_REVIEWED`; draft locales remain qualification-only resources and must not be declared as supported languages in the public package manifest.** This external content-quality gate does not stop implementation/testing of draft resources or unrelated x64 work.
7. No runtime translation API/network. No per-locale XAML tree.
8. Every F01–F26 app-visible string must already have en-US/tr-TR resources before F27 begins.

## Integration points

- `Ornare.App`: `Strings/<BCP47>/Resources.resw`.
- `Ornare.Package`: six `<Resource Language="..."/>` declarations/localizable manifest fields.
- `Ornare.ShellExtension`: native STRINGTABLE/MUI resources only.
- `Ornare.Cli`: unchanged English human strings + locale-neutral JSON.

## Implementation sequence

1. Inventory every app/package/shell user-visible resource key. Audit tool is allowed; it must not generate production translations.
2. Confirm en-US/tr-TR completeness first and fix any F01–F26 hard-coded production-visible strings.
3. Add de-DE/fr-FR/es-ES/pt-BR `.resw` manually. Preserve format placeholders, accelerator semantics and punctuation tokens exactly.
4. Extend the qualification-package manifest language declarations to all six; implement release packaging so public manifest language declarations are filtered by the accepted-locale allowlist (`en-US`, `tr-TR`, plus only `OWNER_ACCEPTED`/`PRO_REVIEWED` new locales). The allowlist is explicit release metadata, not inferred from file presence.
5. Audit current ShellExtension localization:
   - if it already uses native MUI/STRINGTABLE, extend that mechanism;
   - otherwise convert shell titles to language-neutral native resources plus language-specific `.mui` resources built with the Windows resource toolchain;
   - never call WinUI/MRT managed app code from Explorer just for strings.
6. Use Microsoft terminology/glossary/product wording as authoritative vocabulary reference; record unresolved translation decisions in the feature report instead of guessing silently.
7. Launch app under each locale and smoke every new feature surface; validate package/shell title localization separately.
8. Run pseudo/40%-expansion layout QA and 200% text scaling, especially German/French.
9. Record linguistic status per locale as `OWNER_ACCEPTED`, `PRO_REVIEWED`, or `FUNCTIONAL_DRAFT_NOT_LINGUISTICALLY_CERTIFIED`.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| False professional-quality claim | Separate technical resource certification from owner/pro linguistic acceptance. |
| Missing/mismatched placeholders | Automated key/placeholder parity test. |
| Explorer shell becomes heavy | Native resources/MUI only; no managed/WinUI localization load in shell. |
| Layout clipping | 40% expansion + 200% scaling + real locale screenshots. |
| Locale-sensitive machine contracts | IDs/JSON/CLI arguments/invariant serialization remain locale-neutral. |
| CLI bloat | Human CLI stays English-only; no MRT dependency. |

## Required tests

- Key and placeholder parity across all six app locales.
- Qualification package manifest contains exactly six test locales; public-release manifest contains `en-US`, `tr-TR` plus exactly the accepted/reviewed new-locale allowlist and never a draft-only locale.
- App launch/smoke in each locale, including every F01–F26 new UI surface.
- Native Explorer menu title tests for each locale/resource fallback without loading managed App assemblies into Explorer.
- Unsupported locale falls back to English according to package/resource behavior.
- Long-label/200%-text/High Contrast/keyboard checks.
- Turkish search/normalization fixtures remain unchanged.
- Native AOT/package/resource audit for x64 and ARM64 build artifacts.

## Performance requirements

- App resource lookup cost is negligible and no locale-specific XAML duplication is allowed.
- Shell native resource lookup remains within existing menu-construction latency budget.
- CLI receives no localization framework/dependency/startup penalty.

## Approved sources / research

- **WinUI 3 localization:** https://learn.microsoft.com/en-us/windows/apps/winui/winui3/localize-winui3-app
- **MRT Core:** https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/mrtcore/mrtcore-overview
- **Win32 MUI resource management:** https://learn.microsoft.com/en-us/windows/win32/intl/mui-resource-management
- **Win32 STRINGTABLE:** https://learn.microsoft.com/en-us/windows/win32/menurc/stringtable-resource

## Forbidden implementations

- No hard-coded app-visible strings.
- No runtime translation network/API.
- No separate XAML layout per locale.
- No managed/WinUI/Magick/SQLite localization dependency loaded in Explorer.
- No `professionally localized` claim without owner/pro review evidence.

## Feature Definition of Done

- Six-locale technical resource parity/rendering/package/shell tests green.
- Each new locale has explicit linguistic status in report.
- Any locale lacking owner/pro review is marked `FUNCTIONAL_DRAFT_NOT_LINGUISTICALLY_CERTIFIED`, not silently certified.
- Full technical gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F27/` and records affected INV-* invariants, commands/tests, build hashes, localization status and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
