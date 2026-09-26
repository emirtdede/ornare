# Feature Gate Preflight: F23 — Reset from Explorer

**Feature ID:** F23  
**Feature Name:** Reset from Explorer  
**Specification:** `feature-expansion/features/F23_reset-from-explorer.md`  
**Execution Order:** 22 of 27  
**Traceability ID:** `ORN-EXP-F23`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Date:** 2026-09-26  

---

## 1. Scope & Objective

Expose safe reset-to-baseline/default behavior from the existing `Ornare` Explorer flyout without weakening semantic merge, ownership, or cleanup protections.

- **Primary Verb:** Remains "Set Folder Icon" (`CLSID_OrnareCommand`).
- **Flyout Verb:** "Ornare" (`CLSID_OrnareFlyoutCommand`), with sub-commands in fixed order:
  1. "Apply last icon" (`OrnareApplyLastCommand`)
  2. "Undo" (`OrnareUndoCommand`)
  3. "Reset" (`OrnareResetCommand`)
- **Protocol:** `ornare://reset?v=1&path=<url_encoded_path>`
- **CLI Fallback:** `Ornare.App.exe --reset "<folderPath>"`

---

## 2. Invariant & Architecture Checklist

| Rule / Decision | Requirement | Architecture Compliance Plan |
|---|---|---|
| `F23-DEC-01` | Fixed Flyout Child Order | Final children order: 1. `Apply last icon`, 2. `Undo`, 3. `Reset`. Verified via `IEnumExplorerCommand::Next`. |
| `F23-DEC-02` | Golden path command unchanged | "Set Folder Icon" remains independent top-level command (`CLSID_OrnareCommand`). |
| `F23-DEC-03` | Shell sends versioned command only | Shell sends `ornare://reset?v=1&path=...` or `--reset "<path>"`. Zero DB/FS logic in native extension. |
| `F23-DEC-04` | Live semantic merge (no whole-file restore) | Re-reads live `desktop.ini` immediately before commit, strips only Ornare icon semantics, and preserves all unrelated keys/sections (`[ViewState]`, `[HarmlessMetadata]`, `InfoTip`). |
| `F23-DEC-05` | External conflict protection | If external tool modified icon away from Ornare ownership, aborts with `ErrorCode.ExternalConflict` and preserves external state. |
| `F23-DEC-06` | Owned cleanup only | Deletes only proven Ornare-owned artifacts (`.ornare\folder.ico`, empty `.ornare` folder). Never touches non-Ornare files. |
| `F23-DEC-07` | Live NTFS identity & reconciliation | Resolves live NTFS identity; detects renames/moves on same volume; returns `ErrorCode.TargetChanged` if physical identity differs from stored assignment. |
| `F23-DEC-08` | Non-customized folder deterministic no-op | If folder is already Windows default (no Ornare customization), safely returns `ErrorCode.NotCustomized` with zero disk mutation. |
| `F23-DEC-09` | Bilingual localization | Native context menu titles: English "Reset", Turkish "Sıfırla". Tooltip: English "Reset folder icon to Windows default", Turkish "Klasör simgesini Windows varsayılanına sıfırla". |

---

## 3. Test Strategy

1. **Integration Tests (`ResetFromExplorerIntegrationTests`):**
   - Non-customized default folder returns `ErrorCode.NotCustomized` without filesystem mutation.
   - Successful Reset removes Ornare icon semantics and assignment while strictly preserving unrelated `desktop.ini` sections and keys.
   - External modification blocks Reset and returns `ErrorCode.ExternalConflict` without overwriting external state.
   - Renamed/moved folder resolves identity via NTFS (VolumeSerial, FileId64) and executes Reset cleanly.
   - Replaced folder with different NTFS file ID returns `ErrorCode.TargetChanged` and aborts reset.
   - Rapid concurrent duplicate Reset calls serialize cleanly; first succeeds, subsequent call returns `ErrorCode.NotCustomized`.
   - Target validation rejects invalid targets (reparse points, volume roots, system paths) with appropriate `ErrorCode`.
2. **Native COM Flyout Tests (`ShellExtensionFlyoutTests`):**
   - Flyout command enumerates exactly 3 child commands in exact order:
     - Item 0: "Apply last icon" / "Son simgeyi uygula"
     - Item 1: "Undo" / "Geri Al"
     - Item 2: "Reset" / "Sıfırla"
   - Latency budget remains under 10ms.
3. **Full Regression:**
   - All solution tests pass Release.
   - Managed ARM64 Release build clean (0 warnings, 0 errors).
   - Golden path verification clean.
