# F19 — Portable Folder Mode

**Status:** IMPLEMENT-LIMITED  
**Dependencies:** F18; existing relative `.ornare\folder.ico` architecture  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Qualify and expose portability already provided by folder-owned relative icon storage, with a precise identity/path-reconciliation algorithm and no unsupported universal claims.

## Fixed decisions

1. There is no user toggle for core portability: supported customizations store `.ornare\folder.ico` inside the target and `desktop.ini` references it relatively.
2. Publicly supported scope in this feature is **ordinary local fixed-drive NTFS folders**. Same-volume rename/move/nested-parent move is mandatory.
3. Cross-volume move/copy, removable NTFS, FAT/FAT32/exFAT, SMB/WebDAV, cloud placeholders and reparse targets are not promoted to supported by F19.
4. Stable identity for this feature's **NTFS-only** scope is `(VolumeSerialNumber, FileId64)`, where `FileId64 = (nFileIndexHigh << 32) | nFileIndexLow` from `BY_HANDLE_FILE_INFORMATION`/`GetFileInformationByHandle`; Microsoft documents this 64-bit ID + volume serial as the NTFS identity pair. A raw path is an observation, not identity. Do not use `FILE_ID_INFO.FileId128` as the reopen key for F19 NTFS; Microsoft documents `ExtendedFileIdType`/128-bit reopen primarily for ReFS.
5. No whole-volume path scan is ever performed to rediscover a folder.
6. Cross-volume copy/move yields a different volume/file identity and never silently inherits Ornare ownership/history from the original.
7. UI may say exactly `Portable on supported NTFS`; it may not say universally portable.

## Integration points

- `Ornare.Windows`: NTFS capability, `BY_HANDLE_FILE_INFORMATION`, `OpenFileById(FileIdType)`, and final-path normalization.
- `Ornare.Storage`: latest observed path + 32-bit volume serial stored as SQLite INTEGER + `FileId64` stored exactly as an **8-byte little-endian BLOB**; do not store the unsigned 64-bit ID in signed SQLite INTEGER.
- `Ornare.App`: health/status wording; no watcher.

## Implementation sequence

1. Audit every successful apply: durable icon is target-local `.ornare\folder.ico`; `desktop.ini` uses relative icon path; remove any surviving external absolute-source dependency.
2. Open the eligible folder with `FILE_FLAG_BACKUP_SEMANTICS`, call `GetFileInformationByHandle`, build the exact NTFS `FileId64` from `nFileIndexHigh/nFileIndexLow`, and persist `(VolumeSerialNumber, FileId64)` with the latest canonical path.
3. When a known latest path still opens, compare live identity. If equal, update normalized path observation if needed. If identity differs, do not attach old ownership to the new folder at that path.
4. When latest path is missing but the original local NTFS volume is mounted, open a volume hint handle and call `OpenFileById` with `FILE_ID_DESCRIPTOR.Type = FileIdType`, `FileId.QuadPart = stored FileId64`, share mode `FILE_SHARE_READ|FILE_SHARE_WRITE|FILE_SHARE_DELETE`, and `FILE_FLAG_BACKUP_SEMANTICS`; on success obtain the current path with `GetFinalPathNameByHandleW`, normalize the `\\?\`/volume path to Ornare's canonical path form, call `GetFileInformationByHandle` on the reopened handle, require the exact stored volume+FileId64 match, then atomically update latest-path metadata.
5. If open-by-ID/path recovery fails, mark `LocationUnknown`; do not scan the volume/directory tree.
6. When Explorer activation/Manager drag-drop presents a current path, compare live identity to known records; if identity matches, re-associate/update latest path without creating duplicate ownership.
7. Build retained qualification evidence for rename, same-volume move, nested parent move, reboot, source deletion and uninstall persistence.
8. Cross-volume/removable/network/cloud scenarios remain explicitly unqualified until a separate owner-approved qualification feature changes scope.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| Path points to a different folder after move/delete/recreate | Always compare volume+FileId64 before ownership operation. |
| Missing path causes expensive search | Open by file ID only; if it fails, `LocationUnknown`; no disk scan. |
| Copy mistaken for move | Different identity means new target; no automatic history/ownership transfer. |
| Unsupported filesystem semantics | Fail closed outside qualified local fixed NTFS scope. |
| Watcher/background cost | No always-on watcher/service/polling. |

## Required tests

- Fixed NTFS rename, same-volume move and nested-parent move: identity stable, path reconciles.
- Delete/recreate old path with different identity: no ownership misassociation.
- Missing latest path + open-by-ID succeeds -> canonical path recovered.
- Open-by-ID failure -> `LocationUnknown`, zero whole-volume scan.
- Reboot/uninstall/source-delete icon persistence remains green.
- Cross-volume copy fixture is treated as new identity and original history stays with original.
- Unsupported/reparse/network target remains rejected before mutation.

## Performance requirements

- No background watcher/CPU.
- Identity reconciliation only on activation, Manager open/explicit health action or an already-permitted targeted operation.
- No recursive volume enumeration.

## Approved sources / research

- **FolderColorizer reference:** https://github.com/dszharikov/FolderColorizer
- **Crystal Folders reference:** https://github.com/genesistoxical/crystal-folders
- **BY_HANDLE_FILE_INFORMATION / NTFS file ID:** https://learn.microsoft.com/en-us/windows/win32/api/fileapi/ns-fileapi-by_handle_file_information
- **GetFileInformationByHandle:** https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfileinformationbyhandle
- **FILE_ID_DESCRIPTOR / FileIdType:** https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_id_descriptor
- **OpenFileById:** https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-openfilebyid
- **GetFinalPathNameByHandleW:** https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfinalpathnamebyhandlew

## Forbidden implementations

- No universal portability claim.
- No full-disk/volume scan to recover moved folders.
- No always-on watcher.
- No absolute external icon dependency.
- No ownership transfer solely because paths resemble each other.

## Feature Definition of Done

- Fixed local NTFS identity/path matrix green with retained evidence.
- Unsupported/unqualified classes remain fail-closed.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F19/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
