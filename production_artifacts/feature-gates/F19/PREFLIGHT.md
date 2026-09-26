# Preflight Check: F19 — Portable Folder Mode

**Feature ID:** F19  
**Feature Name:** Portable Folder Mode  
**Execution Order:** 18 of 27  
**Traceability ID:** `ORN-EXP-F19`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Specification:** `feature-expansion/features/F19_portable-folder-mode.md`  
**Date:** 2026-09-26  

---

## 1. Scope & Objective

Qualify and expose portability already provided by folder-owned relative icon storage (`.ornare\folder.ico`), backed by a deterministic identity/path-reconciliation algorithm for ordinary local fixed-drive NTFS folders without unsupported universal claims.

The feature subsystem must:
1. Guarantee no user toggle for core portability: all supported customizations store `.ornare\folder.ico` inside the target folder and `desktop.ini` references it relatively (`IconResource=.ornare\folder.ico,0`).
2. Confine publicly supported scope strictly to **ordinary local fixed-drive NTFS folders**. Same-volume rename, move, and nested-parent move are fully qualified.
3. Explicitly fail closed on cross-volume move/copy, removable NTFS, FAT/FAT32/exFAT, SMB/WebDAV, cloud placeholders, and reparse targets.
4. Establish stable NTFS identity as `(VolumeSerialNumber, FileId64)`, where `FileId64 = (nFileIndexHigh << 32) | nFileIndexLow` from `BY_HANDLE_FILE_INFORMATION`/`GetFileInformationByHandle`. Raw path is treated as an observation, not identity.
5. Implement Migration V6: store 32-bit volume serial number as SQLite INTEGER and `FileId64` exactly as an **8-byte little-endian BLOB** (`file_id_blob`) with compound index on `(volume_serial_number, file_id_blob)`.
6. Implement path reconciliation using `OpenFileById` (`FILE_ID_DESCRIPTOR.Type = FileIdType`, `FileId.QuadPart = stored FileId64`, `FILE_FLAG_BACKUP_SEMANTICS`) on a volume hint handle, followed by `GetFinalPathNameByHandleW` (`VOLUME_NAME_DOS`), path prefix normalization, and handle identity verification.
7. If open-by-ID or path recovery fails, mark status as `LocationUnknown`. Never perform a full disk or directory tree scan.
8. Compare live identity when an existing path opens: if identity differs, never attach old ownership to the newly created folder at that path.
9. When Explorer activation or Manager drag/drop presents a folder, match live identity against known assignments to reconnect moved folders without creating duplicate ownership.
10. UI wording is strictly `Portable on supported NTFS`. Universal portability claims are prohibited.

---

## 2. Invariants & Risk Mitigations

| Invariant / Risk | Specification Rule | Planned Mitigation |
|---|---|---|
| `F19-DEC-01` | No user toggle for portability; relative `.ornare\folder.ico` is mandatory | Audit every apply/reset flow; enforce relative path in `desktop.ini`. |
| `F19-DEC-02` | Supported scope: ordinary local fixed-drive NTFS | Validate drive type (`DRIVE_FIXED`) and filesystem (`NTFS`) via `GetVolumeInformationW`. |
| `F19-DEC-03` | Fail closed on non-fixed NTFS, removable, FAT/FAT32, UNC, reparse | Retain strict rejection in `TargetFolderValidator` and `FolderIdentityService`. |
| `F19-DEC-04` | Stable identity is `(VolumeSerialNumber, FileId64)` | Capture `(dwVolumeSerialNumber, nFileIndexHigh, nFileIndexLow)` via `GetFileInformationByHandle`. |
| `F19-DEC-05` | Zero whole-volume disk scanning | Use `OpenFileById` on volume hint; if failure, mark `LocationUnknown` directly. |
| `F19-DEC-06` | Cross-volume copy/move produces new identity; no silent history transfer | Different `(volume, fileId)` pair is treated as a separate uncustomized entity. |
| `F19-DEC-07` | UI wording strictly `Portable on supported NTFS` | Localized strings in `en-US` and `tr-TR` use exact approved phrase. |
| `F19-DEC-08` | Migration V6: 32-bit uint as INTEGER, 64-bit ID as 8-byte LE BLOB | Rollback-safe migration adding `volume_serial_number` and `file_id_blob` columns + index. |
| `F19-DEC-09` | No background watcher / service | Reconciliation executed on-demand during activation, health checks, or explicit operations. |

---

## 3. Test Strategy & Plan

1. **NTFS Identity & Win32 Interop Unit Tests (`FolderIdentityServiceTests` in `Ornare.Windows` / `Ornare.IntegrationTests`):**
   - Volume serial and 64-bit FileId extraction on local NTFS folders.
   - Same-volume rename preserves `(VolumeSerialNumber, FileId64)`.
   - Same-volume move and nested parent move preserve `(VolumeSerialNumber, FileId64)`.
   - `OpenFileById` with volume hint handle successfully reopens moved directory and retrieves canonical path via `GetFinalPathNameByHandleW`.
   - Invalid / deleted file ID cleanly fails without throwing or scanning disk.
   - Local fixed NTFS volume detection returns true for fixed NTFS drive (C:), false for UNC / non-NTFS.
2. **Storage Migration V6 & Identity Lookup Tests (`PortableFolderStorageTests` in `Ornare.Storage.Tests`):**
   - Forward Migration V5 to V6 creates columns and compound index.
   - Injected failure rollback leaves schema at version 5.
   - `UpdateAssignmentIdentityAsync` stores little-endian 8-byte BLOB and 32-bit volume integer.
   - `GetAssignmentByIdentityAsync` retrieves assignment using index seek.
   - `UpdateAssignmentPathAsync` updates path atomically.
3. **Reconciliation Engine Integration Tests (`FolderReconciliationTests` in `Ornare.IntegrationTests`):**
   - Moved folder on same volume: missing old path reconciles to new path via open-by-ID.
   - Deleted old path + recreated new folder at old path with different file ID: does not misassociate ownership; reconciles original folder to its new location if moved.
   - Deletion of folder causes reconciliation to set `LocationUnknown` with 0 disk scans.
   - Re-activation at new path reconciles existing assignment without creating duplicate records.
4. **End-to-End Real Golden Path & Build Matrix:**
   - Full solution Release test suite green.
   - Managed ARM64 build green (0 errors, 0 warnings).
   - Real Golden Path test green.
