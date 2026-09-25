---
description: Windows/Explorer/WinUI platform constraints.
---
# Windows Platform Rules

- Main UI = WinUI 3/.NET 10; shell extension = native C++ `IExplorerCommand`.
- Windows App SDK stable channel only for V1.
- Shell DLL must never initialize WinUI, Magick.NET or SQLite.
- Do not use legacy registry hacks as the primary modern context-menu path.
- Do not restart/kill Explorer in normal flow.
- Preserve existing desktop.ini content and baseline customization.
- Use a folder-owned relative ICO plus canonical library.
- Do not ask for admin rights for protected paths; refuse safely.
- Official launch target Windows 11 25H2+; x64 + ARM64.
- Version-sensitive API/package assumptions must be verified through current Microsoft Learn/upstream docs.
