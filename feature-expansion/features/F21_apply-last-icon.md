# F21 — Apply Last Icon

**Status:** IMPLEMENT  
**Dependencies:** F18  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Apply the most recently durably committed Ornare render recipe to another eligible folder from a lightweight Explorer command without loading Storage or imaging code inside Explorer.

## Fixed decisions

1. Persistent pointer is `LastAppliedRenderKey` (or the exact live-schema equivalent mapped to an immutable recipe record), updated only after a successful durable apply commit.
2. The referenced immutable recipe and all source assets required to reconstruct it are treated as live references for cleanup while `LastAppliedRenderKey` points to them.
3. Preview, import, render-only export and failed/canceled applies never update Last Applied.
4. Explorer never queries SQLite or resolves recipe content. It sends only versioned `command=apply-last` + selected target/context.
5. Final Explorer surface is two top-level app entries:
   - existing **Set Folder Icon** remains the golden-path command;
   - a separate **Ornare** app-attributed flyout supplies secondary commands using `IExplorerCommand::EnumSubCommands`.
6. During F21, the `Ornare` flyout contains only the actually implemented child **Apply last icon**; F22 adds Undo and F23 adds Reset. Do not add disabled/placeholder future children.
7. `EnumSubCommands`, `GetTitle`, `GetState`, `GetFlags` and other menu-construction methods remain tiny/native and do no DB/image/filesystem mutation work.

## Integration points

- `Ornare.Storage`: immutable recipe retention + `LastAppliedRenderKey` setting/ref.
- `Ornare.Core`: `ApplyLastFolderIcon` resolves to the standard apply operation.
- `Ornare.ShellExtension`: app-attributed `Ornare` flyout root + `Apply last icon` child.
- `Ornare.App`: activation routing/single-instance redirect.

## Implementation sequence

1. Ensure F18 schema/cleanup can retain an immutable recipe/source while referenced by Last Applied.
2. Update `LastAppliedRenderKey` in the same durable post-filesystem database commit that records a successful apply; never earlier.
3. Add versioned activation verb `apply-last` and strict target payload validation.
4. Implement the `Ornare` root command with `ECF_HASSUBCOMMANDS` and `EnumSubCommands`; enumerate one child in F21: `Apply last icon`.
5. Child `Invoke` builds tiny activation payload only and activates/redirects Ornare out of process.
6. App resolves Last Applied, validates recipe/source/cache; if derived cache is missing/corrupt but canonical recipe inputs remain, rebuild through normal renderer/cache.
7. Run the existing durable apply transaction and update Last Applied only after success.
8. If pointer is corrupt/unresolvable, clear it transactionally after validation and return typed `NoLastAppliedRecipe`; do not crash or guess.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| Cleanup deletes last recipe/source | Last-applied reference participates in F18 reachability. |
| Explorer slowdown/crash | Static native metadata/subcommand enumeration only; no DB/renderer/mutation in shell. |
| Failed operation becomes Last Applied | Pointer update is part of successful commit only. |
| Missing cache | Rebuild from retained canonical recipe/source outside Explorer. |
| Menu clutter | Golden `Set Folder Icon` stays top-level; secondary commands grouped in one `Ornare` flyout. |
| Unsupported nested menu | One-level `EnumSubCommands` only; no subcommand has further subcommands. |

## Required tests

- No Last Applied state.
- Image recipe, color/template recipe and status/preset recipe.
- Cache removed -> apply-last rebuilds.
- Source/recipe reference retained against cleanup while pointer exists.
- Corrupt pointer -> safe typed state/repair.
- Real mouse Explorer `Ornare > Apply last icon` -> app activation -> durable result.
- Shell latency/menu-construction benchmark; no Storage/Imaging modules loaded in Explorer.
- Same-folder rapid duplicate activation serializes safely.

## Performance requirements

- Menu-construction path stays in existing `<10 ms typical` shell envelope.
- Cache-hit app-side Apply Last skips decode/render.
- No new background process.

## Approved sources / research

- **Packaged Explorer context menu / app-attributed flyout:** https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/integrate-packaged-app-with-file-explorer
- **IExplorerCommand::EnumSubCommands:** https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-iexplorercommand-enumsubcommands
- **Windows App SDK AppInstance:** https://learn.microsoft.com/en-us/windows/apps/develop/launch/multi-instance-apps

## Forbidden implementations

- No SQLite/Imaging/WinUI in Explorer.
- No direct folder mutation from shell DLL.
- No placeholder Undo/Reset commands in F21.
- No Last Applied update on preview/import/failure.

## Feature Definition of Done

- Immutable Last Applied retention tests green.
- Real Explorer E2E evidence retained.
- Shell isolation/performance evidence green.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F21/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
