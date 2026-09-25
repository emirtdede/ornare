# Folder Customization Engine

## 1. Reliability model

A folder customization must not depend on the original user image after application.

Use two layers:

1. **Canonical library asset** in Ornare local app data, keyed by SHA-256, for reuse/history/favorites.
2. **Folder-owned hidden ICO** placed inside the customized folder and referenced relatively by that folder's `desktop.ini`.

This improves survival across source-image deletion, app uninstall (when customizations are kept), and ordinary local rename/move operations.

## 2. `desktop.ini` preservation and ownership

Never delete and recreate an existing `desktop.ini` blindly, and never restore an old full-file snapshot over a newer live file.

Before first Ornare modification:

- read the current file safely with encoding considerations;
- capture the icon-related semantics that Ornare will replace: presence/value of `IconResource` and/or `IconFile`/`IconIndex` as actually used by the chosen implementation;
- capture only relevant attribute state plus whether Ornare later introduced an attribute;
- preserve unrelated sections/keys and current third-party changes;
- maintain enough typed baseline data for a semantic restore.

During every apply/reset/repair, re-read the **current** configuration and merge only Ornare-owned icon semantics. Ornare treats `IconResource`, `IconFile`, and `IconIndex` (when present) as one icon-semantic set for conflict detection and baseline restore; it must not leave competing stale icon directives behind when taking ownership.

If another actor changes the icon semantics while Ornare owns the assignment, classify it as `ExternalIconConflict`; do not silently clobber it and do not offer ordinary one-click Reset as though ownership were still uncontested.

Ownership is grouped into an **ownership epoch**. The epoch begins when Ornare takes/re-takes ownership and stores the then-current external icon semantics as its baseline. Ordinary Ornare-to-Ornare icon changes remain inside that epoch. An external icon conflict ends the uncontested epoch for mutation purposes; old Undo entries cannot be replayed across that boundary.

Conflict behavior:

- **automatic repair:** prohibited;
- **Stop tracking:** explicit action; leave the live external icon/configuration untouched and remove Ornare ownership/tracking only after database/journal consistency checks. Delete an old Ornare-owned folder ICO only if the live configuration provably does not reference it; otherwise prefer a harmless orphan to destructive guessing;
- **Reapply Ornare icon:** explicit action; before taking ownership again, capture the current external icon semantics as the new baseline so a later Reset/Undo can restore what the user actually had immediately before reapplication;
- **direct new Drop/Paste/Picker apply:** counts as explicit intent to set a new icon. Revalidate conflict, adopt the current external icon semantics as the new baseline, then perform the normal journaled apply;
- never resurrect an older baseline over a newer external icon change without explicit user intent.

Use the documented Windows folder-customization mechanism. `SHGetSetFolderCustomSettings` is preferred where its documented path constraints and behavior fit; a direct `desktop.ini` fallback for long paths may ship only after explicit compatibility tests prove it safe.

## 3. Folder-owned asset

Use an Ornare-specific hidden filename that avoids collision, for example an implementation-derived stable name. Do not expose internals in ordinary Explorer views.

The configuration should reference the icon by relative path where Windows supports it.

Do not mark unrelated user files hidden/system.

## 4. Apply safety

- write icon to temp path first;
- fsync/flush/verify as appropriate;
- write updated configuration to temp;
- replace transactionally where possible;
- update required attributes only after files are ready;
- retain typed pre-commit recovery data and journal state until operation is verified/committed; never depend on a whole-file historical `desktop.ini` overwrite for rollback.

## 5. Reset semantics

Reset means “restore the icon semantics that existed before Ornare took ownership,” not “write back an old desktop.ini file” and not always “yellow folder.”

At reset time Ornare re-reads the current configuration, first verifies that the live icon semantics still match the Ornare-owned expected state, preserves unrelated **current** settings, and restores/removes only the icon keys/attributes it owns according to the stored baseline. If live icon semantics changed externally, Reset does not write and the assignment enters `ExternalIconConflict`.

If the pre-Ornare state used:

- another custom icon → restore those prior icon semantics;
- no custom icon → remove only Ornare-owned icon semantics;
- localized/InfoTip/view metadata → leave it untouched;
- attributes that Ornare introduced solely for customization → revert them only when doing so will not break remaining current customization and ownership checks prove it is safe.

After successful reset, remove only Ornare-owned icon assets proven unreferenced. If external icon changes conflict with the stored baseline, surface a repair/conflict decision instead of destructive guessing.

## 6. Uninstall semantics

Default uninstall choice should preserve already-applied folder customizations. Because folder-owned icon files are independent of the app binary, uninstall must not blindly delete them.

If an uninstall path offers “restore all,” it must run a safe pre-uninstall restore process with explicit user intent.

## 7. Cloud/removable folders

Detect common cloud/mirror locations when feasible. Do not claim cross-device portability unless the provider actually syncs `desktop.ini`, hidden ICOs, attributes, and Windows honors them on the destination.

Inform, don't alarm:

`This folder is synced. The icon is applied locally; whether it appears on another PC depends on the sync provider.`

## 8. Windows security hardening

Respect Windows 2026 trust changes for folder customization. Do not clear Mark-of-the-Web globally, relax zones, disable security checks, or alter organization policy.

For unsupported remote locations, fail with a clear localized explanation.

## 9. Repair

Repair may:

- recreate missing Ornare-owned ICO from canonical library/history;
- correct Ornare-managed icon keys while preserving unrelated settings;
- reapply required folder/file attributes;
- trigger a targeted shell refresh.

Repair must not delete global icon caches or restart Explorer unless an explicitly labeled advanced diagnostic path is added later and requires confirmation.
