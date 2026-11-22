# Localization Specification

## 1. Language catalogue

`src/Ornare.Core/LocaleCatalog.cs` is the single source of truth for interface languages. Every picker, the Windows language detection and the resource fallback chain read that list; nothing else may declare its own language array.

The catalogue ships **73 languages**.

| Status | Count | Locales |
| :--- | ---: | :--- |
| Fully translated (`IsComplete = true`) | 6 | `en-US`, `tr-TR`, `de-DE`, `fr-FR`, `es-ES`, `pt-BR` |
| Selectable, English fallback (`IsComplete = false`) | 67 | Every other entry, alphabetical after the six complete ones |

Rule: a language is listed as complete only when every key in the runtime dictionary carries a real translation. A language whose translation is missing stays selectable (users still get the app in English) but must never be presented as translated. `LocalizationParityTests.RuntimeDictionaryCoversExactlyTheCompleteLocales` enforces that the dictionary and the `IsComplete` flags cannot drift apart.

## 2. Resolution rules

`LocaleCatalog.Resolve` applies a fixed order:

1. exact BCP-47 match (`de-DE`);
2. language-only match, first shipped variant wins (`de-AT` -> `de-DE`, `es-AR` -> `es-ES`);
3. `en-US`.

Underscore tags from older configuration files are normalized (`pt_BR` -> `pt-BR`). Whitespace, empty input and `System` resolve through `LocaleCatalog.ResolveSystemLocale`, which reads `CultureInfo.CurrentUICulture` and falls back to English when Windows uses a language the app does not ship.

The first run uses the Windows UI language. The user's explicit choice is persisted in `UserSettings.Language` and is never re-derived from the region or the machine location.

## 3. Where the language is chosen

- **Setup:** the installer lists the same 73 languages in a picker on its first screen, preselects the Windows UI language, falls back to English when nothing matches, and writes the choice to `HKCU\Software\Vellium\Ornare\InstallerLanguage`. Onboarding reads that value before it renders, so the walkthrough opens in the language chosen during setup instead of re-detecting the machine. The setup window itself is translated for the six complete locales and shows English for the rest.
- **First launch:** the onboarding language step lists all 73 languages with their native names, preselects the detected Windows language (or the setup choice, see above), and offers English when nothing matches.
- **Settings:** the language picker offers the same list plus **System default**. Choosing a language applies immediately, re-titles the visible section, rebuilds the settings surface and re-resolves the cloud status badge.
- **Cloud restore:** a restored snapshot carries `UserSettings.Language`; the app applies it after the restore completes.

## 4. Resource layers

Ornare uses two layers on purpose:

| Layer | Location | Used for |
| :--- | :--- | :--- |
| Runtime dictionary | `src/Ornare.App/AppResources.cs`, `AppResources.Extended.cs` | All strings resolved from C#, including strings composed at runtime |
| MRT resources | `src/Ornare.App/Strings/<locale>/Resources.resw` | WinUI/MRT parity and designer tooling |
| Setup table | `src/Ornare.Installer/InstallerStrings.cpp` | The native setup window, which cannot load WinUI resources |

The application layers must contain the same key set per locale. `GetString` returns the requested locale, then `en-US`, then the key itself, so a typo in a key is visible instead of silently empty. The setup table keeps the same 73 tags in the same order and mirrors the translated set: a language selectable in the app is selectable in setup as well.

Adding a string:

1. add the key to every complete locale in `AppResources.cs` (or `AppResources.Extended.cs` for appearance, onboarding and cloud keys) and register it in `ExtendedKeys`;
2. mirror the key in all six `Resources.resw` files;
3. verify with the parity tests, which also check placeholder parity (`{0}`, `{1}` ordering) and reject mojibake.

## 5. Right-to-left languages

Arabic, Hebrew, Persian and Urdu are in the catalogue and resolve to English copy today. `LocaleCatalog.IsRightToLeft` reports them so the layout layer can mirror when a translation lands. The application does not switch `FlowDirection` while copy is English, because mirroring English text inside an RTL layout produces a worse result than an LTR layout with English copy.

## 6. Writing rules

English UI:

- concise sentence case;
- no unnecessary exclamation marks;
- Windows vocabulary (`Settings`, `Open folder`, `Back up now`);
- verbs first in commands.

Turkish UI:

- natural Turkish, not word-for-word English;
- correct dotted/dotless I and locale-sensitive casing;
- concise desktop terminology;
- use the standard Turkish Windows term when one exists (`Gezgin`, `Ayarlar`, `Klasör`).

German, French, Spanish and Portuguese follow the same principles with their own platform vocabulary. Quotation marks and typographic dashes are avoidable: hyphen-minus only, no em dash.

## 7. Formatting and plurals

- Dates and times use `CultureInfo.CurrentCulture` with the Windows short format (`g`).
- Counts are rendered as `Label: value` segments instead of inflected sentences, so English, Turkish, German, French, Spanish and Portuguese never need plural rules in a shared format string. Example: `Folders: 12 - Collections: 3 - Backups: 1`.
- Paths are shown verbatim with wrapping and a tooltip; they are never translated.

## 8. Layout resilience

- Every localized surface must survive at least 40 percent text expansion.
- Onboarding and Settings use star-sized columns with `TextWrapping`; fixed widths are only used for pickers, and the 200 percent scale preset exercises them.
- Interface scale is applied through `ZoomViewport`/`ZoomSurface` so a scaled layout is verified visually, not only in XAML.
- Evidence: `docs/screenshots/manager-settings.png` (Turkish) and `docs/screenshots/manager-settings-en.png` (English) prove the same layout with different string lengths.

## 9. Accessibility names

Automation names, help text and toggle state labels are localized resources like any other string. `ToggleSwitch.OnContent` and `OffContent` are set from `Toggle_On` and `Toggle_Off` so Narrator announces the state in the user's language instead of the default English.

## 10. Verification

Localization is part of the definition of done:

| Gate | Test |
| :--- | :--- |
| Key parity across the six complete locales | `AppResourceKeySetsAreIdenticalAcrossAllSixLocales`, `AllSixLocalesHaveMatchingKeysAndNonEmptyValues` |
| Runtime dictionary equals the complete catalogue | `RuntimeDictionaryCoversExactlyTheCompleteLocales` |
| Every referenced key exists | `EveryReferencedResourceKeyExists` |
| No untranslated XAML literals | `EveryXamlUserFacingLiteralIsLocalizedAtRuntime` |
| No tr/en-only branching | `NoTwoWayTurkishEnglishTernariesRemainInAppCode` |
| Catalogue invariants (73 tags, ordering, fallback) | `LocaleCatalogTests` |
| Setup picker ships the same 73 tags | `InstallerStrings.cpp` table, reviewed against `LocaleCatalog.Entries`, verified in the setup screenshot |

A release candidate also gets a manual pass in Turkish and English with the maximum interface scale, and a scan for emoji and em dash leakage outside the bundled asset library.
