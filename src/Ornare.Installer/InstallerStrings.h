#pragma once

#include <windows.h>

/// <summary>
/// Every user-visible string the setup executable can show. The installer is a plain Win32
/// application, so it cannot reuse the WinUI resource dictionaries; this table is the setup
/// side of the same language catalogue (<c>src/Ornare.Core/LocaleCatalog.cs</c>) and must
/// keep the same 73 tags in the same order.
/// </summary>
struct InstallerStrings
{
    const wchar_t* windowTitle;
    const wchar_t* languageLabel;
    const wchar_t* publisher;
    const wchar_t* description;
    const wchar_t* bullet1;
    const wchar_t* bullet2;
    const wchar_t* bullet3;
    const wchar_t* shortcut;
    const wchar_t* shortcutDescription;
    const wchar_t* ready;
    const wchar_t* starting;
    const wchar_t* verifyingCertificate;
    const wchar_t* extractingPackage;
    const wchar_t* registering;
    const wchar_t* creatingShortcuts;
    const wchar_t* completed;
    const wchar_t* installedAndReady;
    const wchar_t* extractFailed;
    const wchar_t* deploymentFailed;
    const wchar_t* install;
    const wchar_t* cancel;
    const wchar_t* launch;
    const wchar_t* close;
    const wchar_t* retry;
};

/// <summary>A selectable setup language: BCP-47 tag, endonym and translation status.</summary>
struct InstallerLocale
{
    const wchar_t* tag;
    const wchar_t* nativeName;
    bool isTranslated;
};

/// <summary>Number of languages offered by the setup language picker (73).</summary>
int InstallerLocaleCount();

/// <summary>Language at <paramref name="index"/>; out-of-range indexes clamp to English.</summary>
const InstallerLocale& InstallerLocaleAt(int index);

/// <summary>
/// Strings for <paramref name="index"/>. Languages that are selectable but not translated
/// receive the English table, so the setup never shows a half-translated surface.
/// </summary>
const InstallerStrings& InstallerStringsAt(int index);

/// <summary>
/// The language setup should start with: the Windows UI language when it is shipped,
/// otherwise English. Mirrors <c>LocaleCatalog.ResolveSystemLocale</c>.
/// </summary>
int DetectInstallerLocaleIndex();
