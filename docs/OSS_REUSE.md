# Open-Source Reuse and Dependency Plan

Every reuse requires explicit license/provenance review. “On GitHub” is not sufficient.

## 1. Approved package candidates

### Microsoft.WindowsAppSDK
Purpose: WinUI 3 / Windows App SDK runtime and platform.

Baseline researched version: **2.5.1**, stable, released 2026-09-16. Pin the exact release used at bootstrap. Stable channel only for V1.

### Magick.NET-Q16-AnyCPU
Purpose: secure multi-format image decode/normalize/render.

Baseline researched version: **14.17.1** (2026-09). Pin and vulnerability-audit. Apache-2.0 project license. Native dependencies and delegates must also be covered in the release inventory.

### Microsoft.Windows.CsWin32
Purpose: generated strongly typed Win32 interop bindings where useful.

Baseline researched version: **0.3.333**. MIT.

### Microsoft.Data.Sqlite.Core + SQLitePCLRaw.bundle_winsqlite3
Purpose: lightweight SQLite data access.

Baseline: **Microsoft.Data.Sqlite.Core 10.0.12** + **SQLitePCLRaw.bundle_winsqlite3 2.1.11** (reconciled via ADR 0008). Pin both; use Windows system `winsqlite3.dll`; do not rely on optional SQLite extensions without qualification.

### CommunityToolkit.WinUI.Animations
Purpose: selected maintainable animations/behaviors where they beat direct Composition code.

Use **stable 8.2.x only** for V1; do not take 8.3 preview into core production dependencies without ADR.

### WinUIEx
Purpose: narrow window/HWND/AppWindow helpers if Windows App SDK itself lacks a clean API.

Baseline researched version: **2.9.3**, MIT. Keep usage small to reduce dependency surface.

### Fluent System Icons
Purpose: standard action icons.

MIT repository. Use appropriate SVG assets and preserve attribution/license notice. Do not mix indiscriminately with other icon families.

## 2. Source-study/reuse candidates

### `microsoft/vscode-explorer-command`
MIT. Microsoft project that generates Windows 11 shell extension DLLs for sparse/package integration. Use as a high-confidence reference for native `IExplorerCommand` architecture and relevant reusable code after audit. Remove VS Code-specific registry/executable behavior.

### `dszharikov/FolderColorizer`
MIT. Useful reference for hidden per-folder multi-resolution ICO, relative `desktop.ini` references, preservation/restore behavior and Explorer refresh concepts. It uses a classic context-menu path, so do not copy its shell integration as our modern-menu solution.

### RightClickFolderIconTools
MIT code repository, but it contains script/batch-oriented implementation and references third-party templates/assets. Use primarily as feature/algorithm research. Do not import template assets without independent provenance.

## 3. Reference-only / no-copy

### qmatteoq ContextMenuSample
If no explicit compatible license is present at reuse time, study behavior/documentation only. Do not copy source.

### Apple Design Resources
**Do not copy/use in the Windows project.** Apple's license limits design resources to mockups for products running on Apple operating systems and explicitly restricts non-Apple OS use. HIG prose can inform principles; proprietary kits/assets/fonts/symbols cannot be embedded.

## 4. Reuse procedure

Before copying a source file/snippet:

1. capture repository URL, commit SHA, path, license;
2. confirm file is actually covered by that license;
3. identify third-party code/assets inside it;
4. record modifications;
5. add required notice to `THIRD-PARTY-NOTICES.md`;
6. add tests proving behavior in Ornare;
7. avoid importing unrelated architecture/dependencies.

## 5. CI license/security policy

Release CI must produce or validate:

- direct/transitive NuGet list;
- vulnerability audit;
- license inventory;
- native binary inventory;
- OSS notices.

A new dependency requires justification: what problem it solves, why platform code/custom small implementation is worse, package size/runtime cost, maintenance status and license.
