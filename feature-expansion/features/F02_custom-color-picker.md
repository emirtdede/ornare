# F02 — Custom Color Picker

**Status:** IMPLEMENT  
**Dependencies:** F01  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Expose arbitrary user color selection without implementing a custom picker or duplicating palette logic.

## Fixed decisions

1. Use WinUI 3 `ColorPicker`; support spectrum, RGB/HSV and HEX as provided by the platform.
2. Color selection is preview-only until the user explicitly presses **Apply** in the ColorPicker flyout; **Cancel** or `Esc` restores the value captured when the flyout opened and performs no persistence or folder mutation.
3. Persist up to 8 recent custom colors in settings only after the corresponding folder-style apply operation succeeds; canceled selections and failed applies never enter Recents.
4. Custom color uses the exact same F10 palette generator and F01 recipe pipeline.

## Integration points

- `Ornare.App`: ColorPicker flyout/dialog and recent-color chips.
- `Ornare.Core/Imaging`: no new color algorithm.
- `Ornare.App` existing settings abstraction: persist recent custom RGB values in the same user-settings store already used by theme/language preferences; **do not add a SQLite table for Recents**.

## Implementation sequence

1. Use platform `ColorPicker` with alpha disabled because folder base color is opaque; preserve alpha handling only for Background Color feature where explicitly allowed.
2. Bind selected color to a view model/value state; debounce preview rendering at exactly **80 ms** while dragging and cancel every superseded preview task before publishing its result.
3. When the flyout opens, snapshot the current committed color. **Apply** commits the selected color to the pending F01 request and closes the flyout; **Cancel**/`Esc` restores the snapshot and closes it; pointer movement never mutates the folder.
4. On Apply, pass the base color to F01; F01 owns render/cache/apply.
5. After F01 reports successful folder apply, insert that base color at the front of Recents, deduplicate by exact 8-bit RGB value, and cap the list at 8; a failed apply leaves Recents unchanged.
6. Localize accessibility names and verify keyboard navigation through spectrum/sliders/text boxes.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| Applying while dragging | Preview only until explicit commit. |
| Preview task storm | Debounce and cancel stale preview work. |
| Invalid HEX entry | Use platform validation and do not commit invalid state. |
| Accessibility | Use native ColorPicker and provide labeled context/commit controls. |

## Required tests

- HEX/RGB/HSV equivalent colors yield same base value.
- Cancel leaves previous state and folder unchanged.
- Rapid slider movement does not create ICO files/history rows.
- Keyboard-only custom color selection works.
- Recent colors deduplicate and cap at 8.

## Performance requirements

- No custom rendering surface beyond WinUI control.
- No full ICO render during slider movement.
- Picker opening must not initialize Manager-wide data.

## Approved sources / research

- **WinUI ColorPicker:** https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/color-picker

Before copying any source, the agent must re-check the exact upstream commit/license and record provenance according to `../01_OSS_SOURCES_AND_LICENSE_POLICY.md`.

## Forbidden implementations

- Do not build a custom color spectrum.
- Do not apply on `ColorChanged`.
- Do not store recent colors as image assets.

## Feature Definition of Done

- Platform picker used.
- Preview and commit semantics tested.
- Accessibility/localization checks pass.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F02/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
