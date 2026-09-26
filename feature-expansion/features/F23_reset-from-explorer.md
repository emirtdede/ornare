# F23 — Reset from Explorer

**Status:** IMPLEMENT  
**Dependencies:** F21 flyout,F22; existing Reset engine  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Expose safe reset-to-baseline/default behavior from the existing `Ornare` Explorer flyout without weakening semantic merge, ownership or cleanup protections.

## Fixed decisions

1. F23 extends the exact F21/F22 `Ornare` flyout. Final child order is exactly `Apply last icon`, `Undo`, `Reset`.
2. `Set Folder Icon` remains a separate top-level golden command.
3. Explorer sends only versioned `command=reset` + one selected target; no DB/mutation work in shell.
4. Reset re-reads the current `desktop.ini` immediately before commit and changes only Ornare-owned icon semantics; it never restores a stale whole-file snapshot.
5. External icon-semantic conflict across the ownership boundary blocks automatic destructive reset unless the existing ownership rules prove Ornare still owns the current semantics.
6. Unrelated InfoTip/localized/third-party keys survive.
7. Cleanup deletes only proven Ornare-owned unreferenced artifacts and occurs last.

## Integration points

- `Ornare.ShellExtension`: one additional child in existing F21 enumerator.
- `Ornare.App`: reset activation route.
- `Ornare.Core/Storage/Windows`: existing Reset engine only.

## Implementation sequence

1. Add strict versioned `reset` activation payload.
2. Extend existing flyout with `Reset`; no second root/menu architecture.
3. Shell `Invoke` activates app only.
4. App validates live identity/eligibility and invokes existing Reset operation.
5. Operation re-reads current `desktop.ini`, performs semantic ownership merge, journals before visible mutation and cleans owned icon only after durable success.
6. Return typed `Success`, `NotCustomized`, `ExternalConflict`, `TargetChanged`, `UnsupportedTarget`.
7. Refresh only affected shell target after success.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| Third-party desktop.ini clobber | Live semantic merge; never whole-file restore. |
| Foreign icon deleted | Delete only Ornare-owned, proven-unreferenced artifact. |
| Stale path/identity | Revalidate live identity before commit. |
| Explorer instability | Out-of-process operation; shell only activates. |
| Menu duplication | Extend F21 enumerator only. |

## Required tests

- desktop.ini unrelated keys/sections preserved byte/semantic-equivalently as existing policy defines.
- External icon change causes conflict/no destructive overwrite.
- Already-default/non-customized folder is deterministic no-op.
- Rename/same-volume move identity path.
- Final Explorer flyout order has exactly three children.
- Real mouse Explorer Reset E2E.
- Shell latency/module isolation.

## Performance requirements

- Shell work remains minimal and below shared threshold.
- Reset does not initialize Imaging unless existing cleanup validation truly requires metadata; no image decode.

## Approved sources / research

- **Microsoft folder customization:** https://learn.microsoft.com/en-us/windows/win32/shell/how-to-customize-folders-with-desktop-ini
- **Packaged Explorer context menu:** https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/integrate-packaged-app-with-file-explorer

## Forbidden implementations

- No whole `desktop.ini` restore.
- No foreign file deletion.
- No DB/WinUI/Imaging work in Explorer.
- No duplicate secondary menu root.

## Feature Definition of Done

- Semantic preservation/conflict tests green.
- Final one-level Explorer flyout E2E green.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F23/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
