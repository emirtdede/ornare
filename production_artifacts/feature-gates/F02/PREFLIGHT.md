# Feature Preflight: F02 — Custom Color Picker

**Feature ID:** F02  
**Specification:** `feature-expansion/features/F02_custom-color-picker.md`  
**Execution Order:** 5 of 27  
**Traceability ID:** `ORN-EXP-F02`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  

---

## 1. Live Implementation Points

- **`src/Ornare.Core/UserSettings.cs` & `RecentCustomColors` Model:**
  - Add `RecentCustomColors` (`IReadOnlyList<string>`) to `UserSettings`.
  - Provide pure, immutable helper methods for adding a recent color (normalize to `#RRGGBB`, deduplicate, insert at index 0, cap at 8).
- **`src/Ornare.Storage/SqliteStorageService.cs`:**
  - Update `GetUserSettingsAsync` and `SaveUserSettingsAsync` to read/write `RecentCustomColors` via existing `settings` table key `"RecentCustomColors"`.
  - Strictly **no new SQLite table** for recents.
- **`src/Ornare.App/QuickPanelWindow.xaml`:**
  - Add Custom Color button adjacent to built-in color swatches.
  - Define `Flyout` containing:
    - Native WinUI 3 `ColorPicker` (`IsAlphaEnabled="False"`, spectrum, sliders, hex input).
    - Recent custom colors chip bar (up to 8 swatches) for 1-click re-selection.
    - Explicit **Apply** and **Cancel** buttons.
- **`src/Ornare.App/QuickPanelWindow.xaml.cs`:**
  - Snapshot active color on flyout opened.
  - Exactly **80 ms debounce** on `ColorChanged` with `CancellationTokenSource` canceling superseded preview tasks.
  - Render bounded 256x256 PNG preview only; **zero ICO rendering** during slider movement.
  - **Cancel** / `Esc` restores snapshot, closes flyout, and reverts preview without folder mutation.
  - **Apply** closes flyout and stages custom color for commit.
  - On successful folder customizer apply, persist color to `RecentCustomColors` in user settings; failed or canceled applies never enter recents.
- **`src/Ornare.App/Strings/{en-US, tr-TR}/Resources.resw`:**
  - Localized strings: `ColorPicker_CustomColor`, `ColorPicker_RecentColors`, `ColorPicker_Apply`, `ColorPicker_Cancel`.
- **`tests/`:**
  - Unit tests for HEX/RGB consistency, recent colors list deduplication/capping, debounce/cancellation logic.
  - Integration tests for cancel non-mutation and post-apply persistence.

---

## 2. Invariants & Gates

- `INV-EXP-PICKER-01`: Platform WinUI 3 `ColorPicker` used with `IsAlphaEnabled="False"`, spectrum, sliders, and HEX input.
- `INV-EXP-PICKER-02`: Debounce preview rendering at exactly 80 ms while dragging, canceling superseded preview tasks. Zero full ICO rendering during slider movement.
- `INV-EXP-PICKER-03`: Snapshot on open; Apply commits to pending F01 request and closes; Cancel or Esc restores snapshot without mutation.
- `INV-EXP-PICKER-04`: Up to 8 recent custom colors persisted in user settings only upon successful folder apply (never on cancel, never on failed apply). Deduplicated by 8-bit RGB value, capped at 8, MRU order. No SQLite table added for recents.
- `INV-EXP-PICKER-05`: Custom color uses exact F10 palette generator and F01 recipe pipeline.
- `INV-EXP-PICKER-PERF-01`: Picker opening does not load/initialize Manager-wide data.

---

## 3. Supply Chain & Provenance Decisions

- Built entirely on Windows App SDK WinUI 3 `ColorPicker`.
- No custom spectrum, external color picker libraries, or foreign code.

---

## 4. Preflight Baseline

- All 143 solution tests green.
- x64 and ARM64 Release builds green with 0 errors, 0 warnings.
- F18, F09, F10, F01 gates green.
