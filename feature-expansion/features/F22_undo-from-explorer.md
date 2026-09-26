# F22 — Undo from Explorer

**Status:** IMPLEMENT  
**Dependencies:** F21 secondary flyout; existing Undo engine  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Expose the existing safe Ornare Undo operation from the one-level `Ornare` Explorer flyout without moving history/storage/filesystem logic into Explorer.

## Fixed decisions

1. F22 extends the exact F21 `Ornare` flyout; after F22 its children, in fixed order, are `Apply last icon`, `Undo`.
2. `Set Folder Icon` remains a separate top-level golden command.
3. Shell sends versioned `command=undo` + one selected target only; it never queries history/DB.
4. Undo obeys all existing ownership-epoch/external-conflict rules and is allowed to return `ExternalConflict`/`NoUndoAvailable` rather than overwrite non-Ornare state.
5. No subcommand has nested subcommands.

## Integration points

- `Ornare.ShellExtension`: add one native child command to F21 enumerator.
- `Ornare.App`: activation verb/routing.
- `Ornare.Core/Storage/Windows`: existing Undo operation only.

## Implementation sequence

1. Add strict versioned `undo` activation payload.
2. Extend F21 enumerator with one `Undo` child in the fixed order.
3. `Invoke` activates Ornare only; COM boundary catches all exceptions and returns safe HRESULT.
4. App resolves current target identity, then invokes the existing Undo engine by identity/history.
5. Return localized typed outcomes `Success`, `NoUndoAvailable`, `ExternalConflict`, `TargetChanged`, `UnsupportedTarget`; no mutation for non-success outcomes.
6. On success use existing targeted shell refresh and history state update.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| Wrong history/folder | Live target identity revalidation before Undo. |
| External icon overwritten | Ownership epoch/conflict rule is mandatory. |
| Explorer crash/latency | No DB/filesystem mutation in shell; safe COM exception boundary. |
| Menu duplication | Reuse one F21 flyout/enumerator; do not register a second Undo root command. |

## Required tests

- Real Explorer flyout order/title/action.
- Undoable state success.
- No-history no-op.
- External conflict blocks overwrite.
- Rename/same-volume move then Undo resolves same identity.
- Rapid duplicate Undo serializes; second request is deterministic/no harmful double mutation.
- Shell latency and module-load isolation.

## Performance requirements

- Explorer stays under shared shell budget and loads no Storage/Imaging.
- Undo app-side path uses existing engine and no unrelated Manager initialization.

## Approved sources / research

- **Packaged Explorer context menu:** https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/integrate-packaged-app-with-file-explorer
- **IExplorerCommand::EnumSubCommands:** https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-iexplorercommand-enumsubcommands
- **Windows App SDK AppInstance:** https://learn.microsoft.com/en-us/windows/apps/develop/launch/multi-instance-apps

## Forbidden implementations

- No Undo logic/SQLite/desktop.ini mutation in C++ shell DLL.
- No forced overwrite after external conflict.
- No separate test-harness launch accepted as Explorer E2E evidence.

## Feature Definition of Done

- Real mouse Explorer E2E and conflict tests green.
- F21 flyout extended exactly once.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F22/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
