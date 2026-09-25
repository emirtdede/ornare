# Dependency / Platform Version Baseline

Research date: **2026-09-24**. Revalidate during `/bootstrap` and before every release. Exact build dependencies are pinned; do not silently move to preview/RC.

| Component | Verified baseline | Policy |
|---|---:|---|
| .NET SDK | **10.0.401** | Pinned by `global.json`, roll-forward disabled for reproducible development/CI |
| .NET runtime line | **10.0.12 LTS** | Security-serviced .NET 10 line; support through 2028-11-14 |
| Microsoft.WindowsAppSDK | **2.5.1 stable** | Exact central package pin |
| Microsoft.Data.Sqlite.Core | **10.0.12** | Windows-only storage provider; no bundled e_sqlite3 native binary |
| SQLitePCLRaw.bundle_winsqlite3 | **2.1.11** | Uses Windows system `winsqlite3.dll` without shipping another SQLite native engine; runtime/Native AOT validated at Gate 1 (reconciled via ADR 0008) |
| Magick.NET-Q16-AnyCPU | **14.17.1** | Pin; upstream matrix includes Windows x64 + ARM64; strict security policy/resource limits required |
| Microsoft.Windows.CsWin32 | **0.3.333** | Analyzer/source-generator package; `PrivateAssets=all` where appropriate |
| CommunityToolkit.WinUI.Animations | **8.2.251219 stable** | Optional/selective; no preview dependency without ADR |
| WinUIEx | **2.9.3** | Optional/selective helpers only |
| Microsoft.Sbom.DotNetTool | **4.1.5** | Local tool manifest pin for reproducible SBOM generation |
| Windows SDK | **10.0.28000.2705** current stable baseline found | SDK may target earlier supported Windows; do not confuse SDK version with minimum OS |
| Minimum supported OS baseline | **Windows 11 25H2, build family 26200** | Official V1 minimum; Windows 10 unsupported |
| Compatibility target | **Windows 11 26H1, build family 28000 where applicable hardware exists** | Compatibility qualification; 26H1 is a special platform release, not a normal in-place 25H2 feature update |

## Pinning rules

- `Directory.Packages.props` is authoritative for direct NuGet versions.
- Once projects are bootstrapped, commit `packages.lock.json` and use locked restore in CI/release.
- A security servicing update within .NET 10 or an approved package may be adopted after tests and baseline update; do not wait for a normal feature release when a security fix is required.
- A newer preview/RC does not supersede stable policy.
- If bootstrap proves a listed package version incompatible with the pinned WinUI/.NET toolchain, do not improvise: document evidence and create an ADR for the replacement.
