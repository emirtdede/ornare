# ADR 0001 — Native WinUI 3 application

Status: Accepted

## Decision

Use WinUI 3 + .NET 10 for the main Windows-only application, with native C++ for Explorer `IExplorerCommand` integration.

## Rationale

Ornare is deeply Windows-specific and benefits from direct Windows App SDK, package identity, UI Automation, Mica/Acrylic, accessibility and shell integration. Tauri/Electron would add WebView/browser layers without providing meaningful cross-platform leverage because folder customization itself is Windows-specific.
