# Schema, Upgrade, Uninstall and Recovery Standard

## 1. Versioned state

Version independently:

- SQLite schema;
- image pipeline output format/version;
- folder-owned asset naming format;
- activation payload protocol;
- desktop.ini icon semantics owned by Ornare.

Do not infer compatibility only from application semantic version.

## 2. Database migrations

- forward migrations are explicit, ordered and transactional where SQLite permits;
- migration is tested from every publicly supported prior schema to the target schema;
- migration never mutates folder-owned icons before the DB step required to recover that mutation exists;
- a failed migration leaves the prior usable DB or a recoverable backup/transaction state;
- no silent destructive schema reset.

## 3. Folder customization migrations

Changing hidden ICO filename or desktop.ini representation requires:

1. read current assignment and live folder state;
2. preserve unrelated current settings;
3. create new asset/config safely;
4. verify;
5. update tracking state;
6. delete old Ornare-owned asset only when no longer referenced.

## 4. Uninstall

Default: keep already-applied icons functional. Uninstall must not traverse arbitrary user folders deleting hidden assets.

Optional restore-all requires explicit user intent and a pre-uninstall executable path that can safely reach tracked folders. Inaccessible/missing folders are reported, never treated as successful restores.

## 5. Application-state recovery

On startup, inspect unfinished operation journal before accepting new mutation operations. Recovery is idempotent and bounded; do not scan the whole filesystem.

If DB is unavailable/corrupt, the quick panel may enter read-only/error mode rather than apply new mutations without history/recovery guarantees.

## Ownership-epoch recovery

Migrations must preserve `ownership_epoch_id` semantics. If older data cannot prove an uncontested ownership epoch, migrate conservatively to a health/conflict state instead of enabling destructive Reset/Undo. History from a prior external-conflict boundary remains informational and is not automatically replayable.
