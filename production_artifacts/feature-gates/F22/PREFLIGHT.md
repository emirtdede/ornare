# Feature Gate Preflight: F22 — Undo from Explorer

**Feature ID:** F22  
**Feature Name:** Undo from Explorer  
**Specification:** `feature-expansion/features/F22_undo-from-explorer.md`  
**Execution Order:** 21 of 27  
**Traceability ID:** `ORN-EXP-F22`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Date:** 2026-09-26  

---

## 1. Scope & Objective

Expose the existing safe Ornare Undo operation from the one-level `Ornare` Explorer flyout without moving history/storage/filesystem logic into Windows Explorer.

- **Primary Verb:** Remains "Set Folder Icon" (`CLSID_OrnareCommand`).
- **Flyout Verb:** "Ornare" (`CLSID_OrnareFlyoutCommand`), with sub-commands in fixed order:
  1. "Apply last icon" (`OrnareApplyLastCommand`)
  2. "Undo" (`OrnareUndoCommand`)
- **Protocol:** `ornare://undo?v=1&path=<url_encoded_path>`
- **CLI Fallback:** `Ornare.App.exe --undo "<folderPath>"`

---

## 2. Invariant & Architecture Checklist

| Rule / Decision | Requirement | Architecture Compliance Plan |
|---|---|---|
| `F22-DEC-01` | Fixed Flyout Child Order | Children order: 1. `Apply last icon`, 2. `Undo`. Verified via `IEnumExplorerCommand::Next`. |
| `F22-DEC-02` | Golden path command unchanged | "Set Folder Icon" remains independent top-level command. |
| `F22-DEC-03` | Shell sends versioned command only | Shell sends `ornare://undo?v=1&path=...` or `--undo "<path>"`. Zero DB/FS logic in native extension. |
| `F22-DEC-04` | Safe conflict handling | Returns `ExternalConflict` or `NoUndoAvailable` instead of overwriting non-Ornare state. |
| `F22-DEC-05` | No nested subcommands | `OrnareUndoCommand::EnumSubCommands` returns `E_NOTIMPL`. |
| `F22-DEC-06` | Rapid duplicate serialization | `ConcurrentDictionary<string, SemaphoreSlim>` ensures second rapid call serializes and yields clean `NoUndoAvailable` without double mutation. |
| `F22-DEC-07` | NTFS identity reconciliation | Validates target identity; if folder renamed/moved on same volume, reconciles path and restores baseline. |
| `F22-DEC-08` | Bilingual localization | Native context menu titles: English "Undo", Turkish "Geri Al". |

---

## 3. Test Strategy

1. **Integration Tests (`UndoFromExplorerIntegrationTests`):**
   - No history available on pristine folder returns `NoUndoAvailable` without filesystem mutation.
   - Successful Undo restores pre-customization semantics and removes folder assignment.
   - External modification blocks Undo and returns `ExternalConflict` without overwriting external state.
   - Renamed/moved folder resolves identity via NTFS (VolumeSerial, FileId64) and performs Undo.
   - Rapid concurrent duplicate Undo calls serialize; first succeeds, second safely no-ops.
   - Target folder validation: Reparse points, non-existent directories, volume roots rejected with appropriate `ErrorCode`.
2. **Native COM Flyout Tests (`ShellExtensionFlyoutTests`):**
   - Flyout command enumerates exactly 2 child commands in exact order:
     - Item 0: "Apply last icon" / "Son simgeyi uygula"
     - Item 1: "Undo" / "Geri Al"
   - Latency budget remains under 10ms.
3. **Full Regression:**
   - All solution tests pass Release.
   - Managed ARM64 Release build clean (0 warnings, 0 errors).
   - Golden path verification clean.
