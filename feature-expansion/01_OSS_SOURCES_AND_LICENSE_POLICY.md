# OSS / Platform Sources and Reuse Policy

Research snapshot: **2026-09-26**. The coding agent must re-check the upstream repository/license/version immediately before copying code or adding a package.

| Source | URL | License / authority | Ornare use |
|---|---|---|---|
| Magick.NET | https://github.com/dlemstra/Magick.NET | Apache-2.0 | Existing production dependency; keep for decode/resize/composite/ICO/PNG. |
| System.CommandLine | https://github.com/dotnet/command-line-api | MIT / .NET Foundation | Approved for CLI parsing; pin a stable release after AOT/security verification. |
| WinUI ColorPicker | https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/color-picker | Microsoft platform API | Use directly; do not build a custom color spectrum control. |
| WinUI drag and drop | https://learn.microsoft.com/en-us/windows/apps/develop/data/drag-and-drop | Microsoft platform API | Use `AllowDrop`, `DragOver`, `Drop`, `DataPackageView`; no third-party DnD framework. |
| MRT Core | https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/mrtcore/mrtcore-overview | Microsoft platform API | Existing localization/resource mechanism. |
| WinUI localization | https://learn.microsoft.com/en-us/windows/apps/winui/winui3/localize-winui3-app | Microsoft platform guidance | `.resw`, BCP-47 folders, manifest/UI strings. |
| Win32 MUI resource management | https://learn.microsoft.com/en-us/windows/win32/intl/mui-resource-management | Microsoft Win32 guidance | F27 native ShellExtension localized resources without managed UI in Explorer. |
| Win32 STRINGTABLE | https://learn.microsoft.com/en-us/windows/win32/menurc/stringtable-resource | Microsoft Win32 resource format | Native shell command strings. |
| Folder customization via desktop.ini | https://learn.microsoft.com/en-us/windows/win32/shell/how-to-customize-folders-with-desktop-ini | Microsoft Win32 guidance | Authoritative behavior reference. |
| SHChangeNotify | https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shchangenotify | Microsoft Win32 API | Existing targeted Explorer refresh mechanism/reference. |
| GetFileInformationByHandleEx | https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getfileinformationbyhandleex | Microsoft Win32 API | Folder identity/capability work. |
| OpenFileById | https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-openfilebyid | Microsoft Win32 API | F19 same-volume NTFS identity-to-current-path reconciliation. |
| GetFinalPathNameByHandleW | https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfinalpathnamebyhandlew | Microsoft Win32 API | Recover final path from a reopened identity handle. |
| Packaged Explorer context menu | https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/integrate-packaged-app-with-file-explorer | Microsoft platform guidance | F21–F23 modern Explorer command/flyout architecture. |
| IExplorerCommand::EnumSubCommands | https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-iexplorercommand-enumsubcommands | Microsoft Win32 API | One-level Ornare secondary flyout children. |
| Windows App SDK AppInstance | https://learn.microsoft.com/en-us/windows/apps/develop/launch/multi-instance-apps | Microsoft platform API | Existing activation redirection/single-instance reference. |
| AppExecutionAlias | https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-uap5-appexecutionalias | Microsoft package-manifest schema | CLI alias integration (`uap5:AppExecutionAlias`). |
| Fluent UI System Icons | https://github.com/microsoft/fluentui-system-icons | MIT | Approved source for a small audited subset of status/priority SVG glyphs; pin commit and preserve notice. |
| OKLab reference | https://bottosson.github.io/posts/oklab/ | Authoritative algorithm reference | F10 conversion matrices/formulas; copy formulas only with engineering provenance. |
| WCAG non-text contrast | https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html | W3C guidance | F10 required graphical-indicator contrast target. |
| ReplaceFileW | https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew | Microsoft Win32 API | F13 safe replacement of existing local export destination. |
| MoveFileExW | https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefileexw | Microsoft Win32 API | F13 same-directory publish/replace helper where appropriate. |
| FolderColorizer | https://github.com/dszharikov/FolderColorizer | MIT | Source-study/selective reuse candidate for color palette, multi-resolution ICO, relative folder icon and tests; do not replace Ornare mutation architecture. |
| Crystal Folders | https://github.com/genesistoxical/crystal-folders | MIT | Reference/test-oracle for drag/drop, portable folder behavior, cache refresh; do not take as dependency. |
| Drop Icons | https://github.com/genesistoxical/drop-icons | MIT | Reference/test-oracle for image→ICO behavior; Magick.NET remains production engine. |
| ImageToIcon | https://github.com/koryboc/ImageToIcon | GPL-3.0 | **Reference only** for output/CLI/size behavior; do not copy GPL source into closed-source Ornare. |
| Svg.Skia | https://github.com/wieslawsoltes/Svg.Skia | MIT | Not approved by default; only after an ADR proves Magick.NET cannot meet a concrete SVG requirement. |
| SkiaSharp | https://github.com/mono/SkiaSharp | MIT | Same conditional rule as Svg.Skia; do not add merely for convenience. |
| Folder11 | https://github.com/icon11-community/Folder11 | repository has asset-use restrictions; verify current terms | Reference-only for visual/size concepts; do not copy restricted icon assets. |
| Folder11 size issue | https://github.com/icon11-community/Folder11/issues/193 | issue/reference | Evidence that small-size optical variants are a nontrivial unsolved product problem. |

## Specific source-reuse decisions

- **Reuse existing libraries before writing codecs/renderers/parsers.** Magick.NET, WinUI, MRT Core and SQLite already solve infrastructure that Ornare should not reimplement.
- **Do not import another app's mutation engine.** Ornare's durable journal, semantic `desktop.ini` merge, ownership epochs and recovery are stricter and remain authoritative.
- **Do not add Svg.Skia/SkiaSharp now.** Additional native binaries increase package size, memory, ARM64 surface and security maintenance with no proven blocker.
- **Do not use GPL source code.** ImageToIcon may be used as a black-box behavioral comparison on developer machines only.
- **The uploaded `Windows_11_FOLDER.svg` is not release-approved by default.** Its provenance/license is not established in this pack. It may be used for local development comparison; production must either (a) document a compatible license/ownership, or (b) ship an independently authored Ornare template with no copied proprietary asset geometry.


## Internet research protocol when a feature has no ready-code link

The agent has internet access and must not guess. For any missing implementation primitive, perform this sequence before writing infrastructure from scratch:

1. **Name the primitive precisely** — e.g. “WinUI packaged app execution alias”, “multi-frame ICO encode”, “Windows folder identity”, not the product feature name.
2. **Search authoritative platform docs first** — Microsoft Learn / Windows App SDK / .NET docs using the exact API or behavior plus the project baseline (`Windows App SDK 2.5.1`, `.NET 10`, `WinUI 3`, `Native AOT`, `ARM64`).
3. **Search upstream GitHub second** — prefer repositories owned by Microsoft/.NET Foundation or the library author; do not use forks/scrapes when upstream exists.
4. **Evaluate reuse candidate before copying:** current license, exact commit/tag, last meaningful maintenance, open security advisories, Windows 11 compatibility, x64/ARM64 support, Native AOT/trimming behavior, transitive native binaries, package size/startup implications and whether the repository solves the exact primitive rather than a superficially similar problem.
5. **Classify the result:** `PLATFORM API`, `APPROVED DEPENDENCY`, `SELECTIVE SOURCE REUSE`, `REFERENCE/TEST ORACLE ONLY`, or `REJECT`.
6. **Prefer the narrowest solution:** a platform API beats a new package; an existing approved Ornare dependency beats a second graphics/storage stack; a 30-line audited MIT helper beats importing a whole competing application.
7. **If no suitable reusable implementation exists, write the smallest Ornare-specific implementation** in the project that owns that responsibility and add direct tests for the risk that forced custom code.
8. **Record evidence** in the current feature report: search terms, authoritative URLs, candidate repositories considered, license result, why reuse/custom code was chosen, and exact upstream commit for anything copied.

Do not turn internet research into dependency accumulation. A library is not approved merely because it exists or has many GitHub stars.
