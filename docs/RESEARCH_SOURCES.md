# Research Sources — verified 2026-09-24

Prefer these primary sources during implementation; re-check date-sensitive details.

## Apple design principles / restrictions

- Apple HIG — Materials: https://developer.apple.com/design/human-interface-guidelines/materials
- Apple HIG — Branding: https://developer.apple.com/design/human-interface-guidelines/branding
- Apple HIG — Color: https://developer.apple.com/design/human-interface-guidelines/color
- Apple HIG — Typography: https://developer.apple.com/design/human-interface-guidelines/typography
- Apple HIG — Drag and drop: https://developer.apple.com/design/human-interface-guidelines/drag-and-drop
- Designing for macOS: https://developer.apple.com/design/human-interface-guidelines/designing-for-macos/
- Apple Design Resources: https://developer.apple.com/design/resources/
- Apple Design Resources License: https://developer.apple.com/support/downloads/terms/apple-design-resources/Apple-Design-Resources-License-20230621-English.pdf

Key constraint: Apple's Design Resources license limits the resources to UI mockups for products running on Apple operating systems and restricts use for non-Apple OS products. Do not ship/use the kits in Ornare.

## Microsoft / Windows

- Windows App SDK downloads: https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads
- Windows App SDK release channels: https://learn.microsoft.com/windows/apps/windows-app-sdk/release-channels
- Windows 11 Home/Pro lifecycle: https://learn.microsoft.com/lifecycle/products/windows-11-home-and-pro
- File Explorer modern context menu integration: https://learn.microsoft.com/windows/apps/desktop/modernize/integrate-packaged-app-with-file-explorer
- `desktop.ini` folder customization: https://learn.microsoft.com/windows/win32/shell/how-to-customize-folders-with-desktop-ini
- System backdrops: https://learn.microsoft.com/windows/apps/develop/ui/system-backdrops
- Theming: https://learn.microsoft.com/windows/apps/develop/ui/theming
- WinUI app localization: search current Microsoft Learn for “Localize your WinUI app” / MRT Core before implementation.
- Windows App Development CLI: https://learn.microsoft.com/windows/apps/dev-tools/winapp-cli/
- winapp UI Automation: https://learn.microsoft.com/windows/apps/dev-tools/winapp-cli/ui-automation
- Windows testing/performance documentation: https://learn.microsoft.com/windows/apps/develop/testing/ and Windows Performance Toolkit docs.
- Microsoft Learn MCP: https://learn.microsoft.com/training/support/mcp
- MCP endpoint: https://learn.microsoft.com/api/mcp

## Antigravity

- Google Codelab — Antigravity rules/skills/workflows: https://codelabs.developers.google.com/sdd-adk-antigravity
- Google Codelab — autonomous pipelines: https://codelabs.developers.google.com/autonomous-ai-developer-pipelines-antigravity
- Google Codelab — getting started IDE/skills: https://codelabs.developers.google.com/getting-started-agy-ide
- Google Developer Knowledge MCP + Antigravity config: https://codelabs.developers.google.com/developer-knowledge-mcp-antigravity

## OSS repositories

- VS Code Explorer command: https://github.com/microsoft/vscode-explorer-command
- FolderColorizer: https://github.com/dszharikov/FolderColorizer
- Magick.NET: https://github.com/dlemstra/Magick.NET
- CsWin32: https://github.com/microsoft/CsWin32
- Windows Community Toolkit: https://github.com/CommunityToolkit/Windows
- WinUIEx: https://github.com/dotMorten/WinUIEx
- Fluent System Icons: https://github.com/microsoft/fluentui-system-icons
- GitHub MCP Server: https://github.com/github/github-mcp-server

## Source discipline

When docs disagree with cached knowledge, use the current official page, note the applicable product/version, and update this file/ADR if it affects a frozen decision.

## Enterprise secure-development / supply-chain standards

- Microsoft Security Development Lifecycle overview: https://www.microsoft.com/securityengineering/sdl
- Microsoft SDL practices: https://www.microsoft.com/securityengineering/sdl/practices
- Microsoft SDL assurance overview: https://learn.microsoft.com/compliance/assurance/assurance-microsoft-security-development-lifecycle
- NIST SP 800-218 SSDF v1.1 (final): https://csrc.nist.gov/pubs/sp/800/218/final
- NIST SSDF project/publications: https://csrc.nist.gov/projects/ssdf
- NuGet package lock files / PackageReference: https://learn.microsoft.com/nuget/consume-packages/package-references-in-project-files
- NuGet dependency vulnerability auditing: https://learn.microsoft.com/nuget/concepts/auditing-packages
- Microsoft SBOM Tool: https://github.com/microsoft/sbom-tool
- .NET code analysis: https://learn.microsoft.com/dotnet/fundamentals/code-analysis/overview
- MSVC C++ security best practices: https://learn.microsoft.com/cpp/security/security-best-practices-for-cpp
- Control Flow Guard: https://learn.microsoft.com/cpp/build/reference/guard-enable-guard-checks

## Current platform/version verification (2026-09-24)

- .NET 10 download/status (SDK 10.0.401, runtime 10.0.12): https://dotnet.microsoft.com/download/dotnet/10.0
- .NET 10 release index: https://github.com/dotnet/core/blob/main/release-notes/10.0/README.md
- Windows 11 release information/build families: https://learn.microsoft.com/windows/release-health/windows11-release-information
- Windows 11 lifecycle: https://learn.microsoft.com/lifecycle/products/windows-11-home-and-pro
- Windows SDK downloads: https://learn.microsoft.com/windows/apps/windows-sdk/downloads
- `SHGetSetFolderCustomSettings`: https://learn.microsoft.com/windows/win32/api/shlobj_core/nf-shlobj_core-shgetsetfoldercustomsettings
- `SHFOLDERCUSTOMSETTINGS`: https://learn.microsoft.com/windows/win32/api/shlobj_core/ns-shlobj_core-shfoldercustomsettings

## SQLite / architecture refinement (2026-09-24)

- Microsoft Learn — Windows app SQLite tutorial: Windows supplies SQLite and can be used instead of packaging another native SQLite engine: https://learn.microsoft.com/windows/apps/develop/data-access/sqlite-data-access
- Microsoft Learn — custom Microsoft.Data.Sqlite native providers, including `winsqlite3`: https://learn.microsoft.com/dotnet/standard/data/sqlite/custom-versions
- NuGet — Microsoft.Data.Sqlite.Core 10.0.12: https://www.nuget.org/packages/Microsoft.Data.Sqlite.Core/10.0.12
- NuGet — SQLitePCLRaw.bundle_winsqlite3 2.1.11: https://www.nuget.org/packages/SQLitePCLRaw.bundle_winsqlite3/2.1.11
- Magick.NET upstream support matrix confirms Windows x64 and ARM64 support for Q16/AnyCPU: https://github.com/dlemstra/Magick.NET
- GitHub official MCP server — remote endpoint and read-only mode: https://github.com/github/github-mcp-server

- Microsoft Learn — custom SQLite versions/providers (`bundle_winsqlite3`): https://learn.microsoft.com/dotnet/standard/data/sqlite/custom-versions
- Microsoft Learn — C/C++ buffer-overrun mitigations (`/GS`, `/sdl`, CFG, DEP, ASLR, CET): https://learn.microsoft.com/windows/win32/secbp/avoiding-buffer-overruns
- MSVC Control Flow Guard: https://learn.microsoft.com/cpp/build/reference/guard-enable-guard-checks
- NIST SP 800-218 Rev.1 / SSDF v1.2 initial public draft (informational until final): https://csrc.nist.gov/pubs/sp/800/218/r1/ipd
- .NET 10 package list JSON/vulnerability reporting: https://learn.microsoft.com/dotnet/core/tools/dotnet-package-list

- Windows app code-signing options (Store/Azure Artifact Signing/CA): https://learn.microsoft.com/windows/apps/package-and-deploy/code-signing-options
- Microsoft Store MSIX certification/publishing signing behavior: https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/app-certification-process
