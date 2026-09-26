# Feature Gate Report: F02 — Custom Color Picker

**Feature ID:** F02  
**Specification:** `feature-expansion/features/F02_custom-color-picker.md`  
**Execution Order:** 5 of 27  
**Traceability ID:** `ORN-EXP-F02`  
**Status:** `PASS`  
**External Dependency Status:** Physical ARM64 hardware certification is `NOT PROVEN / PENDING HARDWARE CERTIFICATION` (x64 Release certified; ARM64 compilation verified with 0 errors).

---

## 1. Executive Summary

Feature F02 exposes arbitrary user color selection via the native Windows App SDK WinUI 3 `ColorPicker` control without custom color spectrums or duplicate palette math.

Key achievements:
1. **Native WinUI 3 ColorPicker Control:** Configured with `IsAlphaEnabled="False"` (opaque folder base color), full spectrum, RGB/HSV sliders, and HEX input box (`INV-EXP-PICKER-01`).
2. **80 ms Debounced Preview Rendering:** During slider dragging or hex typing, preview tasks are debounced at exactly 80 ms using `CancellationTokenSource`. Superseded preview tasks are cancelled immediately before publishing. Renders bounded 256x256 PNGs only; **zero ICO files** are generated during slider motion (`INV-EXP-PICKER-02`).
3. **Snapshot and Cancel Rollback:** Snapshot of the staged icon, process result, preview bitmap, and stage is captured when the flyout opens. Pressing **Cancel** or `Esc` restores the snapshot and closes the flyout, executing zero folder mutations and zero persistence. Pressing **Apply** stages the custom color for folder commit (`INV-EXP-PICKER-03`).
4. **Recent Custom Colors Settings Persistence:** Persists up to 8 recent custom colors in user settings (`UserSettings.RecentCustomColors`) via the existing `settings` key-value table. Custom colors are added strictly upon successful folder apply in MRU order, deduplicated by 8-bit RGB value, and capped at 8 items. Canceled selections and failed applies never enter recents. Strictly **no new SQLite table** added (`INV-EXP-PICKER-04`).
5. **Shared Pipeline Architecture:** Custom colors feed directly into the authoritative F10 `FolderPaletteGenerator` (OKLab/OKLCH gamut mapping) and F01 `FolderTemplateRenderer` / F18 render cache (`INV-EXP-PICKER-05`).
6. **Zero Manager Data Initialization on Open:** Flyout opening accesses only local user settings; it does not load or scan library asset or folder assignment databases (`INV-EXP-PICKER-PERF-01`).
7. **Bilingual Localization:** Full English (`en-US`) and Turkish (`tr-TR`) strings for Custom Color, Recent Colors, Apply Color, and Cancel in `Resources.resw`.

---

## 2. Invariants & Requirements Verification

| Invariant | Requirement | Verification Evidence |
|---|---|---|
| `INV-EXP-PICKER-01` | Native WinUI 3 ColorPicker with alpha disabled, spectrum, sliders, and HEX | Verified in `QuickPanelWindow.xaml` and x64 UI build. |
| `INV-EXP-PICKER-02` | 80 ms debounced preview, canceled superseded tasks, zero ICO files during dragging | Verified in `RapidSliderMovementSimulationCancelsSupersededWorkAndGeneratesZeroIcoFiles`. |
| `INV-EXP-PICKER-03` | Snapshot on open; Apply stages color; Cancel/Esc restores snapshot without mutation | Verified in `CancelLeavesTargetFolderAndRecentSettingsUnchanged` and `QuickPanelWindow.xaml.cs`. |
| `INV-EXP-PICKER-04` | Up to 8 recent custom colors persisted in user settings only on successful apply; no new SQLite table | Verified in `WithRecentCustomColorDeduplicatesAndPreservesMruOrder`, `WithRecentCustomColorCapsAtEightEntries`, `SqliteStorageServicePersistsAndLoadsRecentCustomColorsCorrectly`, and `FailedApplyLeavesRecentCustomColorsUnchanged`. |
| `INV-EXP-PICKER-05` | Custom color uses exact F10 palette generator and F01 recipe pipeline | Verified in `HexRgbEquivalenceYieldsIdenticalPaletteAndSemanticRoles` and `SuccessfulCustomColorApplyCreatesDurableIcoAndPersistsToRecents`. |
| `INV-EXP-PICKER-PERF-01` | Picker opening does not load/initialize Manager-wide data | Audited in `OnCustomColorFlyoutOpened`: reads `settings` table key-value only. |

---

## 3. Concrete Implementation Files

1. **`src/Ornare.Core/UserSettings.cs`:**
   - Added `RecentCustomColors` (`IReadOnlyList<string>`) and `WithRecentCustomColor(string hex)` helper with normalization, deduplication, and 8-item capacity capping.
2. **`src/Ornare.Core/FolderJsonContext.cs`:**
   - Added source generation attributes for `List<string>` and `string[]` for Native AOT safety.
3. **`src/Ornare.Storage/SqliteStorageService.cs`:**
   - Updated `GetUserSettingsAsync` and `SaveUserSettingsAsync` to read/write `RecentCustomColors` via key `"RecentCustomColors"` in the existing `settings` table.
4. **`src/Ornare.App/QuickPanelWindow.xaml`:**
   - Added circular `CustomColorButton` with palette glyph (`&#xE790;`).
   - Defined `CustomColorFlyout` with mini-preview, `ColorPicker`, recent color chips, and Apply/Cancel buttons.
5. **`src/Ornare.App/QuickPanelWindow.xaml.cs`:**
   - Implemented `IDisposable` on `QuickPanelWindow` for deterministic cleanup of `CancellationTokenSource` and temp files.
   - Implemented snapshot capture on flyout open and rollback on cancel.
   - Implemented 80 ms debounced preview rendering via `FolderTemplateRenderer.RenderPreviewPng`.
   - Implemented post-apply persistence of custom colors in `CommitStagedIconAsync`.
6. **`src/Ornare.App/Strings/{en-US, tr-TR}/Resources.resw`:**
   - Localized resource keys `ColorPicker_CustomColor`, `ColorPicker_RecentColors`, `ColorPicker_Apply`, `ColorPicker_Cancel`.
7. **`tests/Ornare.Imaging.Tests/CustomColorPickerTests.cs`:**
   - 3 tests: HEX/RGB equivalence, valid 256x256 PNG preview without ICO, rapid slider debounce cancellation and zero ICO files.
8. **`tests/Ornare.Storage.Tests/RecentCustomColorsTests.cs`:**
   - 3 tests: MRU order and deduplication, 8-entry capacity cap, SQLite settings persistence round-trip.
9. **`tests/Ornare.IntegrationTests/CustomColorApplyIntegrationTests.cs`:**
   - 3 tests: Cancel non-mutation, durable apply with recents persistence, failed apply leaves recents unchanged.

---

## 4. Verification Evidence & Test Results

### 4.1. Unit and Integration Tests
- `tests/Ornare.Imaging.Tests/CustomColorPickerTests.cs`: 3 passed, 0 failed.
- `tests/Ornare.Storage.Tests/RecentCustomColorsTests.cs`: 3 passed, 0 failed.
- `tests/Ornare.IntegrationTests/CustomColorApplyIntegrationTests.cs`: 3 passed, 0 failed.
- Full Solution Test Suite (`Ornare.slnx`): **152 passed, 0 failed, 0 skipped**.
  - `Ornare.Core.Tests`: 26 passed
  - `Ornare.Storage.Tests`: 21 passed (+3 for F02)
  - `Ornare.Imaging.Tests`: 62 passed (+3 for F02)
  - `Ornare.IntegrationTests`: 34 passed (+3 for F02)
  - `Ornare.UiTests`: 9 passed

### 4.2. Platform Compilation Gates
- **Windows 11 x64 Release:** Build succeeded with 0 warnings, 0 errors.
- **Windows 11 ARM64 Release:** All 6 managed projects compiled cleanly for `win-arm64` with 0 warnings, 0 errors via `scripts/build-managed-arm64.ps1`.

### 4.3. Real x64 Golden Path E2E
- Ran `scripts/test-real-golden-path.ps1`:
  - Verified folder customization applied with full attributes (`ReadOnly`, `Hidden`, `System`).
  - `desktop.ini` preserved without clobbering unrelated sections or InfoTip.
  - Shell notification dispatched with 0 Explorer restarts.
  - Result: Complete success.

---

## 5. Traceability & Requirements Status

- `ORN-EXP-F02`: **PASS**
- Requirement updated in `docs/REQUIREMENTS_TRACEABILITY.md`.
- Next Feature in `02_EXECUTION_ORDER.md`: **F06 — Contain / Cover / Fill**.
