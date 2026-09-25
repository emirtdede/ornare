# Product Specification — Ornare V1

## 1. Product statement

Ornare is a Windows 11 utility that removes the technical friction from durable folder icon customization. It converts a user-provided image into a high-quality multi-resolution Windows icon, applies it safely to one selected folder, and keeps the customization working even if the original image disappears.

The product must feel like a missing, exceptionally polished operating-system utility rather than a feature-heavy customization suite.

## 2. Core promise

**Any supported local image → a durable folder icon in a few seconds.**

Time-to-icon is the primary product KPI. Reliability after application is the second.

## 3. Primary user story

As a Windows user, I can right-click one local folder, choose `Set Folder Icon`, and then drop, browse for, or paste an image so that the folder changes immediately without requiring me to create an ICO, restart Explorer, or keep the source image.

## 4. V1 workflows

### 4.1 Explorer quick workflow

1. User right-clicks exactly one supported folder.
2. Windows 11 primary context menu shows **Set Folder Icon**.
3. Ornare opens a compact quick surface near the invocation context on the same monitor.
4. The surface shows the selected folder identity and one dominant image action.
5. User drops an image, clicks to browse, or presses Ctrl+V.
6. Ornare validates, normalizes, renders, writes the folder-owned ICO, updates customization atomically, refreshes Explorer, and commits history.
7. The surface displays a restrained success state with **Undo**.
8. It auto-closes after a short delay unless the user is interacting with it.

No Apply button is part of the golden path.

### 4.2 Reset

Reset restores the folder customization state that existed immediately before Ornare first modified it in the current customization lineage. If there was a third-party custom icon, reset restores it. If there was no customization, reset returns to the system default.

### 4.3 Undo

Undo reverses the latest successful Ornare change using recorded history and the same durable journal, ownership, conflict-detection and reconciliation guarantees as Apply.

### 4.4 Full manager

The full manager provides:

- Recent icon assets;
- Favorites;
- Library of user-provided canonical assets;
- Customized folders;
- per-folder history;
- repair status/actions;
- Settings;
- diagnostics export.

The manager is secondary. It must not become required for the primary quick workflow.

## 5. V1 included scope

- Windows 11 modern top-level folder context-menu command.
- One-folder selection only.
- Drag and drop.
- Local file picker.
- Clipboard bitmap and copied local image file.
- Secure decoding and format validation.
- Multi-resolution ICO generation.
- Aspect ratio preservation; default visual policy = contain, not destructive crop.
- Durable relative folder-owned ICO.
- Canonical content-hash library for reuse.
- Immediate targeted Explorer refresh.
- Recent.
- Favorites.
- Reset.
- Undo.
- History.
- Customized folder list.
- Repair engine.
- Light, Dark, High Contrast, system default theme.
- English (`en-US`) and Turkish (`tr-TR`), system preference by default.
- x64 and ARM64.
- Store-ready MSIX bundle.
- Local diagnostics export.

## 6. Explicit V1 exclusions

- batch assignment;
- multi-folder context action;
- image-file context action that asks for a destination folder;
- folder colors;
- stickers/emoji/text overlays;
- icon marketplace;
- online image search;
- AI generation;
- automatic poster/cover detection;
- recursive processing;
- folder automation rules;
- account or cloud sync;
- remote image URL download;
- guaranteed SMB/UNC/WebDAV/HTTP folder customization;
- Windows 10.

If an agent tries to implement excluded features before V1 gates pass, reject the scope expansion.

## 7. Supported environments

Official launch baseline:

- Windows 11 25H2 x64;
- Windows 11 25H2 ARM64.

Compatibility target:

- Windows 11 26H1 on actual supported commercially representative hardware/architecture available to the project. Microsoft positions 26H1 as a platform release for specific silicon rather than a normal in-place 25H2 feature update, so Ornare does not invent a mandatory 26H1 x64/ARM64 pair.

Windows 11 24H2 may work but is not a launch support commitment because its Home/Pro servicing end is imminent relative to the planned release period. See `PLATFORM_COMPATIBILITY.md`.

## 8. Filesystem support

### Fully supported

Writable local folders on ordinary local filesystems where Windows folder customization semantics work reliably.

### Supported with caveats and test coverage

- local OneDrive folders;
- locally mirrored Dropbox/Google Drive folders;
- removable/local external drives.

The customization travels only insofar as the filesystem/provider preserves the hidden icon and `desktop.ini` semantics.

### Unsupported/blocked or explicit caveat

- protected Windows locations requiring elevation;
- Windows installation/system folders;
- HTTP/WebDAV paths;
- untrusted remote locations;
- network paths where Windows security policy prevents `desktop.ini` customization.

Ornare must not weaken global Windows security policy to make an unsupported folder work.

## 9. Privacy contract

V1 has:

- no login;
- no account;
- no ads;
- no analytics SDK;
- no behavior telemetry;
- no image upload;
- no remote processing;
- no AI requirement.

The core application must remain useful offline.

## 10. Success metrics

Engineering gates are defined elsewhere, but product-level targets are:

- user reaches a valid image action without instruction;
- no ICO knowledge required;
- no Explorer restart required in normal operation;
- source image deletion does not break the folder icon;
- local rename/move retains customization;
- standard uninstall can preserve applied customizations;
- errors are explained in user language, not HRESULT-first language.

## 11. V1.1/V2 candidate backlog

Only after V1 release quality:

- batch assignment;
- image-context workflow;
- auto cover detection;
- semantic folder colors;
- command palette if manager complexity justifies it;
- portable export/import tooling;
- richer repair/reconciliation.
