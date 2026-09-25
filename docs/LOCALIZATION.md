# Localization Specification

## 1. Languages

V1 ships:

- English (United States): `en-US`
- Turkish (Türkiye): `tr-TR`

English is fallback. Both are first-class release languages.

## 2. Windows-native resource system

Use WinUI 3 / MRT Core resource conventions and `.resw` resources. All user-visible UI strings must have resource keys. No production-visible text literals in XAML or C# except technical/non-user values.

Expected resource structure follows supported WinUI tooling, e.g. language-specific `Strings/<language>/Resources.resw` under the application project.

## 3. Automatic language

Default behavior: follow the user's Windows preferred application language/resource resolution. Do not force Turkish based on region or machine location.

If a manual app-language choice is shipped, choices are:

- System default (default)
- English
- Türkçe

Use supported Windows App SDK language override APIs only. Test restart requirements and communicate them accurately.

## 4. Writing rules

English UI:

- concise sentence case;
- no unnecessary exclamation marks;
- use Windows vocabulary.

Turkish UI:

- natural Turkish, not word-for-word English;
- correct dotted/dotless I and locale-sensitive casing;
- concise desktop terminology;
- avoid untranslated technical terms where a standard Turkish Windows term exists.

## 5. Formatting

Use locale-aware APIs for dates, times, numbers and file sizes. Do not manually concatenate localized sentence fragments.

## 6. Layout resilience

- test at least 40% text expansion;
- no fixed-width labels where avoidable;
- paths use trimming/tooltip without hiding folder identity;
- button labels must not clip in Turkish;
- resource pseudo-localization should be part of pre-release QA if tooling allows.

## 7. Accessibility names

Automation names/help text are localized resources too.
