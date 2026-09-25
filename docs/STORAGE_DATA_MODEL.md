# Storage and Data Model

## 1. Principles

- Metadata in SQLite through `Microsoft.Data.Sqlite.Core` + `SQLitePCLRaw.bundle_winsqlite3`, using Windows system `winsqlite3.dll`; do not bundle a second SQLite native engine unless an ADR proves it necessary. Storage SQL must not rely on optional extensions such as FTS/JSON/R*Tree unless the supported Windows baseline is explicitly qualified for that extension.
- Binary canonical ICO/assets on filesystem, not as SQLite BLOBs.
- Per-folder durable icon lives with the folder.
- No cloud database.
- No user-account identity.
- Schema migrations are explicit and tested.

## 2. App-local layout

Conceptual layout:

```text
LocalAppData/Ornare/
  data/
    ornare.db
  library/
    <hash>/
      icon.ico
      optional-source-metadata.json
  diagnostics/
    (user-triggered/export staging only)
```

Exact packaged paths must follow Windows App SDK/MSIX conventions.

## 3. Core tables

### `icon_assets`

- `id`
- `content_hash`
- `pipeline_version`
- `canonical_icon_path`
- `source_format`
- dimensions / alpha metadata where useful
- `created_at`
- `last_used_at`

### `folder_assignments`

- `id`
- current `folder_path`
- optional `volume_id` / filesystem identity
- optional `file_id`
- `icon_asset_id`
- `ownership_epoch_id`
- Ornare-owned relative filename
- health/status
- `created_at`
- `updated_at`

### `folder_baselines`

Captures only the pre-Ornare **owned semantics** required to restore safely:

- folder assignment id / identity;
- `ownership_epoch_id`;
- which icon keys existed (`IconResource` and/or `IconFile`/`IconIndex`) and their prior values;
- relevant folder/desktop.ini attributes and whether Ornare introduced them;
- baseline schema/version and observation timestamp;
- conflict/version token/hash for icon-related semantics where useful.

Do **not** use a historical full-file `desktop.ini` blob as a later overwrite source. Reset re-reads the live file and merges only owned semantics so unrelated changes made after initial customization survive.

### `history`

- folder identity
- `ownership_epoch_id`
- previous asset/baseline reference
- new asset reference
- operation kind
- timestamp
- undo eligibility/version; Undo is valid only inside the current uncontested ownership epoch

### `favorites`

- asset id
- created/order metadata

### `operation_journal`

Crash-consistency journal for cross-filesystem/SQLite mutations:

- `operation_id`
- operation kind
- target path + optional durable filesystem identity
- target assignment/baseline identifiers
- intended/new asset reference
- prior owned icon semantics needed for rollback
- state (`Prepared`, `FilesystemCommitted`, `Committed`, `NeedsRepair`)
- timestamps
- safe error/recovery metadata

Journal entries are created before the first user-visible filesystem mutation and are processed idempotently on next launch if unfinished.

### `settings`

Only application settings that cannot cleanly live in Windows app settings/resources.

## 4. Concurrency and durability

Enable SQLite WAL where compatible/appropriate, but avoid background writers. Use short transactions.

Because SQLite and arbitrary folder files do not share one transaction coordinator, DB failure after filesystem apply is resolved through the durable operation journal and reconciliation algorithm rather than pretending a DB rollback can undo disk state. Recovery must be idempotent.

## 5. Privacy

Do not store original user image bytes unless a future explicit feature requires it. Store only what is required for icon reuse/recovery. Paths are personal data and stay local.

Diagnostics must redact paths by default unless the user explicitly opts to include them.

## 6. Cleanup

Canonical asset garbage collection may remove an asset only when:

- no tracked folder assignment references it;
- no favorite references it;
- history retention rules permit deletion;
- operation is crash-safe.

Never scan/delete hidden folder-owned icons outside Ornare tracking without provenance verification.

## 7. Ownership epochs

An `ownership_epoch_id` changes when Ornare takes ownership from an untracked state or re-takes ownership after an external icon conflict. Reset restores the baseline of the current epoch. Undo/history never crosses an external-conflict epoch boundary automatically. This prevents stale history from overwriting a newer icon chosen outside Ornare.
