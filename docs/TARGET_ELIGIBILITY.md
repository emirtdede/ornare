# Folder Target Eligibility Policy

V1 does not attempt to customize every filesystem namespace object. It supports a narrow, testable set and fails **before mutation** outside it.

## 1. Supported target predicate

A folder is eligible only when all applicable checks succeed:

1. Explorer selection resolves to exactly one filesystem directory.
2. The app process reopens/revalidates the directory after activation.
3. The target exists and remains the same filesystem object at the pre-commit identity check.
4. The location is local, not UNC/SMB/WebDAV/HTTP/virtual shell namespace.
5. The target is not a volume root.
6. The target itself is not a symlink/junction/reparse-point directory in V1. If this policy changes, it requires an ADR and race/cleanup tests.
7. The standard user has the real ACL/share access required to create/update the Ornare-owned ICO and folder customization metadata.
8. The filesystem/location belongs to a release-qualified support class.
9. Path length is supported by the selected customization implementation per `LONG_PATH_COMPATIBILITY.md`.
10. No concurrent identity/configuration conflict invalidates ownership immediately before commit.

## 2. Filesystem support classes

### Fully supported at launch

- ordinary local **NTFS** directories on Windows 11 25H2 x64/ARM64 that satisfy the predicate;
- fixed-drive NTFS is the primary golden path;
- removable NTFS may be promoted to fully supported only after the same apply/reset/rename/move/unplug/replug qualification suite passes.

### Qualified/caveated

- local cloud-synced directories only when the actual target is locally available, not an unsupported reparse target, and behaves as a normal qualified filesystem directory. Cross-device icon propagation is **not** promised because providers differ in syncing hidden files, `desktop.ini`, and attributes.

### Not promised in V1

- FAT/FAT32/exFAT until explicit qualification promotes them;
- UNC/SMB shares;
- WebDAV/HTTP-backed folders;
- virtual shell folders without ordinary filesystem paths;
- volume roots;
- symlink/junction/reparse-point target directories;
- protected locations that require elevation or unsafe ACL/security changes.

## 3. Important read-only-attribute distinction

The Windows **directory ReadOnly/System attribute used for shell folder customization is not equivalent to NTFS write permission**. Ornare must not reject a directory merely because its ReadOnly attribute bit is set. Eligibility is based on actual filesystem access/ACL behavior plus safe ownership rules. Ornare may set/revert shell-related attributes only according to `FOLDER_CUSTOMIZATION.md` and stored ownership information.

## 4. Revalidation and TOCTOU

Eligibility at context-menu time is advisory only. The app repeats validation and identity checks before mutation. If the folder changes, becomes a reparse point, disappears, or resolves to a different identity, abort before writing or enter the documented recovery path if commit already started.

## 5. User experience

Unsupported targets receive one concise localized explanation. Do not offer an elevation button, registry/security-policy workaround, or “force” mode in V1.
