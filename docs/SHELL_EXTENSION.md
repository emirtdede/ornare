# Windows 11 Shell Extension Specification

## 1. Objective

Expose `Set Folder Icon` in the modern Windows 11 File Explorer context menu for exactly one supported folder, while minimizing risk to Explorer stability.

## 2. Implementation

- Native C++.
- COM `IExplorerCommand` pattern.
- Packaged registration using `windows.fileExplorerContextMenus` / package identity as supported by the chosen Windows App SDK/package model.
- Use Microsoft's MIT-licensed `vscode-explorer-command` as an architecture/source reference after line-level audit.

Do not use legacy registry-only shell hacks as the primary Windows 11 experience.

## 3. Command visibility/state

Enable only when:

- exactly one filesystem folder is selected;
- target can be represented and passed safely;
- selection is not a known unsupported virtual shell object.

For multiple selected folders, hide or disable in V1; do not silently apply only to the first.

## 4. Invocation

On invoke:

1. capture selected folder;
2. capture sufficient monitor/cursor/window context for quick-panel placement if reliable;
3. invoke/activate Ornare app;
4. return immediately.

No image or database work occurs in the DLL.

## 5. Stability rules

- no unbounded blocking calls;
- no network calls;
- no UI creation;
- no exceptions crossing COM boundaries;
- validate every pointer/result;
- fail closed: if state is uncertain, omit/disable command rather than destabilize Explorer;
- log only minimal diagnostic events using safe mechanisms; avoid synchronous log-file contention from Explorer.

## 6. Performance budget

Typical command-state work target: under 10 ms. This is an internal engineering target; measure on representative machines. Shell extension must not materially increase context-menu latency.

## 7. Architecture targets

Build native binaries for x64 and ARM64. Package registrations and COM identities must be deterministic and version-safe.

## 8. Verification

- repeated right-click stress test;
- context menu open/close loop;
- Explorer restart/session restart;
- package update;
- app not running / app already running;
- unsupported virtual locations;
- long Unicode paths;
- folder names containing Turkish/non-Latin characters;
- x64 and ARM64.

Any Explorer crash attributable to Ornare is a release blocker.
