# Project Decision Ledger

These are frozen unless an ADR supersedes them.

- Name/codename: **Ornare**.
- Framework: WinUI 3, C#, .NET 10 LTS.
- Windows App SDK: **2.5.1 stable** baseline pinned centrally; upgrades require baseline revalidation.
- OS: Windows 11 25H2 (build family 26200) launch baseline; Windows 10 unsupported. 26H1 is compatibility-qualified on applicable real supported hardware.
- Architectures: x64 + ARM64 first-class on 25H2; do not fabricate unavailable 26H1 architecture targets.
- Shell: native C++ `IExplorerCommand` DLL.
- Package: MSIX bundle / Store primary.
- Image engine: Magick.NET, strict policy.
- Win32 bindings: CsWin32.
- DB: `Microsoft.Data.Sqlite.Core` + `SQLitePCLRaw.bundle_winsqlite3` using Windows system `winsqlite3.dll`; no EF, no bundled `e_sqlite3` by default, and no dependency on optional SQLite extensions unless qualified.
- UI: original Ornare design system + XAML + Composition.
- Optional helpers: CommunityToolkit stable only; WinUIEx selectively.
- No web UI stack.
- Theme: system default; Light/Dark/High Contrast supported.
- Languages: en-US + tr-TR; system language by default.
- Storage: canonical hashed asset library + per-folder hidden relative ICO.
- No source-image dependency after apply.
- No normal-flow Explorer restart/cache deletion.
- V1 single-folder only.
- V1 offline/local-only/no telemetry.
- UI has no Apply button on golden path.
- Apple HIG principles may inform design; Apple assets are prohibited.
- Scripts may automate engineering chores; scripts may not generate product code/UI/assets.

- Cross-filesystem/SQLite mutation model: durable operation journal + idempotent reconciliation.
- Reset semantics: merge/restore only Ornare-owned icon semantics; never overwrite a newer whole `desktop.ini` snapshot.
- Quality gates: fail-closed P0/P1 policy in `QUALITY_GATES.md`.
- .NET SDK: 10.0.401 pinned via `global.json` at research date; security servicing baseline revalidated before release.
