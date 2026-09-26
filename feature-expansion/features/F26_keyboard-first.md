# F26 — Keyboard-First Workflow

**Status:** IMPLEMENT  
**Dependencies:** all new UI surfaces stable  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Make Quick Panel and Manager fully operable with keyboard and assistive technology without adding a parallel UI.

## Fixed decisions

1. Tab order follows visual/logical order; no keyboard traps.
2. Use only WinUI routed `KeyboardAccelerator`/access-key mechanisms. The required V1 accelerator set is exactly the set in Fixed Decision 3; do not add global hooks or additional undocumented shortcuts.
3. Required V1 shortcuts are exact: `Ctrl+O` opens image picker when Quick Panel or image editor owns focus; `Ctrl+V` invokes Ornare paste only when focus is not inside an editable text control; `Ctrl+Z` invokes Ornare Undo only when focus is not inside an editable text control and the active folder context has an undoable operation (editable controls retain their native text-undo); `Ctrl+F` focuses/selects the Library search box only while Manager is active; `Esc` closes the topmost transient flyout/dialog or cancels a pre-commit editor operation; `Enter` invokes the focused control's normal primary action and is never globally remapped.
4. Focus must never disappear after async loading/success/error transitions.

## Integration points

- `Ornare.App`/`DesignSystem`: XAML focus properties, accelerators, automation names/help text.
- No Core/Storage changes expected.

## Implementation sequence

1. Inventory every interactive control in Quick Panel and Manager. Tab navigation order is defined by logical XAML tree order; **do not use positive `TabIndex` values**. If visual and logical order disagree, restructure the XAML/control grouping so both orders agree rather than patching navigation with numeric indices.
2. Add keyboard accelerators to command surfaces and localized accessible labels/tooltips.
3. On async transition, retain focus when the focused element remains enabled/visible. If it disappears or becomes disabled, move focus by this fixed order: operation error/success action if one exists, otherwise the primary input/drop-zone action, otherwise the first enabled control in logical XAML order; announce status through the existing WinUI accessibility live-region/status mechanism.
4. Ensure drag/drop-only functionality has keyboard/file-picker equivalent.
5. Test at 200% text scale and High Contrast so focus visuals remain visible.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| Shortcut collision | Use standard app-scoped combinations and respect text-box editing. |
| Focus loss after rerender | Avoid replacing entire visual tree; explicit focus restoration only when necessary. |
| Color-only state | Text/icon/automation state accompanies color. |
| Drag-only action | Every drop action has keyboard alternative. |

## Required tests

- Full Quick Panel golden path keyboard-only.
- Manager navigation, search, tags, collections, export keyboard-only.
- Narrator/UIA smoke.
- High Contrast focus visibility.
- No shortcut fires while typing where inappropriate.

## Performance requirements

- No polling/hotkey hook; WinUI routed accelerators only.
- No measurable performance impact.

## Approved sources / research

- **WinUI KeyboardAccelerators:** https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.keyboardaccelerators

Before copying any source, the agent must re-check the exact upstream commit/license and record provenance according to `../01_OSS_SOURCES_AND_LICENSE_POLICY.md`.

## Forbidden implementations

- No global low-level keyboard hook.
- No invisible focus target hacks.
- No mouse-only feature.

## Feature Definition of Done

- Keyboard acceptance matrix green.
- Accessibility smoke green.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F26/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
