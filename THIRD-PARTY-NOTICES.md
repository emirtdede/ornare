# Third-Party Notices — Pre-Code Baseline

This file is **not release-final**. It records dependencies/reference sources approved by specification as of 2026-09-24. Release automation/review must regenerate and reconcile the complete direct/transitive/native/copied-source inventory and include required license/copyright text.

## Planned runtime/build dependencies

| Component | Baseline | License / role |
|---|---:|---|
| Microsoft.WindowsAppSDK | 2.5.1 | Microsoft-distributed Windows App SDK; runtime/platform dependency |
| Microsoft.Data.Sqlite.Core | 10.0.12 | MIT; managed SQLite ADO.NET layer |
| SQLitePCLRaw.bundle_winsqlite3 | 2.1.11 | Apache-2.0; uses Windows system `winsqlite3.dll` (reconciled via ADR 0008) |
| Microsoft.Windows.CsWin32 | 0.3.333 | MIT; source generator/analyzer, normally `PrivateAssets=all` |
| Magick.NET-Q16-AnyCPU | 14.17.1 baseline | Apache-2.0; image processing; native/transitive inventory required |
| CommunityToolkit.WinUI.Animations | 8.2.251219 | MIT; optional selective UI animation helper |
| WinUIEx | 2.9.3 | MIT; optional selective window helper |
| Microsoft.Sbom.DotNetTool | 4.1.5 | MIT; build tooling only, not shipped runtime dependency |

## Approved source-reference candidates

- `microsoft/vscode-explorer-command` — MIT — reference/reuse candidate for `IExplorerCommand` shell-extension architecture. Any copied source must record exact commit/file and preserve required notices.
- `dszharikov/FolderColorizer` — MIT — reference/reuse candidate for folder-customization behavior. Any copied source must record exact commit/file and preserve required notices.

## Asset policy

Do not copy third-party icon packs, templates, fonts, Apple Design Resources, SF Symbols, SF Pro, or repository assets merely because surrounding code is open source. Asset licenses are reviewed separately from code licenses.

## Release rule

Before Gate 5, replace this baseline with the actual shipped inventory, transitive/native components, copied-source provenance, modifications, required license texts/notices, and file hashes where policy requires them.
