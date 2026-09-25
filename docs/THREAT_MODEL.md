# Threat Model

Threat modeling is updated when architecture, input formats, privilege, distribution or folder mutation semantics change.

## Assets to protect

- integrity of user folders/files and existing `desktop.ini` customizations;
- Explorer availability/stability;
- local user privacy (paths/images/history);
- package/update integrity;
- application settings/database/history;
- signing and release credentials.

## Trust boundaries

1. **Explorer -> shell extension**: Explorer invokes native third-party code in a high-availability process.
2. **Shell extension -> app activation payload**: selected path/identity is untrusted until revalidated.
3. **Clipboard/files -> image decoder**: hostile binary/image/vector content.
4. **App -> target filesystem**: user-controlled paths, reparse points, cloud placeholders, permissions and concurrent mutation.
5. **App -> SQLite/app-local files**: crash consistency and migration boundary.
6. **Build -> third-party packages/native DLLs**: software supply chain.
7. **Release pipeline -> signing/Store**: credential and artifact integrity.

## STRIDE-oriented risks and controls

### Spoofing

- A path may point to a different object after activation. Revalidate identity/attributes immediately before commit; use volume/file identity where available.
- Ornare-owned hidden assets require provenance marker/naming + tracked assignment, never filename-only trust.

### Tampering

- Existing desktop.ini may change concurrently. Re-read before commit; merge only owned icon semantics; detect conflicting icon edits and fail/offer repair rather than overwriting silently.
- DB/filesystem split-brain is handled by operation journal/reconciliation.

### Repudiation

No telemetry is required. Local operation correlation IDs and explicit diagnostic export allow support without permanently collecting personal data.

### Information disclosure

- No image upload/network decode.
- Path data remains local.
- Persistent diagnostics are off by default; exported diagnostics redact paths and never include image bytes.

### Denial of service

- Shell callbacks have strict latency/no-heavy-work limits.
- Image dimensions/area/frame/resource/time limits prevent decompression bombs.
- Limit concurrent imaging operations.
- Corrupt DB does not block Explorer; app can enter recovery mode.

### Elevation of privilege

- Standard user only.
- Protected locations fail safely; no automatic UAC/elevated helper.
- Do not modify global Windows trust policies or security zones.

## Abuse cases

- crafted SVG references network/file resources;
- massive image dimensions/animation frames;
- symlink/junction swaps between check and write;
- target folder loses actual write permission/disappears mid-commit;
- malicious desktop.ini content/encoding edge cases;
- icon file name collision;
- repeated context-menu activation causing race/reentrancy;
- corrupted SQLite/migration interrupted;
- package update changes hidden icon naming but leaves old assignments.

Each abuse case must map to a test or a documented platform limitation before release.
