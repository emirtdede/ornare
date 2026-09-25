---
name: windows-shell-extension
description: Use for Windows 11 File Explorer context-menu integration, COM IExplorerCommand, package registration, app activation, architecture, or Explorer stability work.
---
# Windows Shell Extension Skill

- Use Microsoft Learn MCP to verify current package/context-menu APIs.
- Inspect `microsoft/vscode-explorer-command` MIT source as the preferred source reference.
- Keep DLL native C++ and tiny.
- Exactly one folder in V1; hide/disable ambiguous selections.
- Do no image/database/UI work in Explorer.
- Catch/contain all failures at COM boundary.
- Build x64 + ARM64.
- Stress Explorer context menu and monitor crashes/hangs.
- Record CLSID/package identity decisions in ADR/manifest docs.
