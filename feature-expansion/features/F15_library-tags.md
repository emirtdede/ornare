# F15 — Library Tags

**Status:** IMPLEMENT  
**Dependencies:** F18  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Add normalized many-to-many tags to Library assets with deterministic Unicode identity, database-enforced integrity and no Quick Panel startup cost.

## Fixed decisions

1. Tags preserve user display casing but uniqueness uses Ornare's exact invariant normalization defined below.
2. Display name is trimmed to **1–40 Unicode scalar values**; Unicode control characters are rejected.
3. Maximum **32 tags per asset** and **5000 tag rows total**.
4. Tag deletion removes tag relationships only; it never requests asset deletion/cleanup.
5. Every SQLite connection created by Ornare Storage must execute/verify `PRAGMA foreign_keys=ON`; junction rows use declared foreign keys with `ON DELETE CASCADE`.
6. Exact tag normalization algorithm, in order:
   - trim leading/trailing Unicode whitespace by Rune/Unicode whitespace semantics;
   - normalize to Unicode **NFKC** (`NormalizationForm.FormKC`);
   - apply `.ToUpperInvariant()`;
   - normalize to **NFKC** again;
   - reject empty result and any Unicode scalar whose category is `Control`;
   - use that resulting string as `normalized_name`.
   This is an Ornare-defined invariant identifier rule, not a claim of full linguistic Unicode case-folding.

## Integration points

- `Ornare.Storage`: versioned forward migration adding `tags` and `asset_tags`.
- `Ornare.Core`: one shared `LibraryNameNormalizer`/tag validation contract reused by F16/F17.
- `Ornare.App`: Manager-only flat tag editor/filter.

## Implementation sequence

1. Add migration:
   - `tags(id TEXT PRIMARY KEY, display_name TEXT NOT NULL, normalized_name TEXT NOT NULL UNIQUE, created_utc INTEGER NOT NULL)`;
   - `asset_tags(asset_hash TEXT NOT NULL REFERENCES assets(asset_hash) ON DELETE CASCADE, tag_id TEXT NOT NULL REFERENCES tags(id) ON DELETE CASCADE, created_utc INTEGER NOT NULL, PRIMARY KEY(asset_hash,tag_id))`;
   adapt only the exact existing asset PK column name if the live schema differs, without changing semantics.
2. Ensure Storage connection initialization turns foreign keys on and has an automated assertion test.
3. Implement the exact NFKC/invariant normalization and Unicode-scalar length/control checks in one shared Core utility.
4. Implement create/attach/detach/delete repository methods with parameterized SQL and transactions; enforce caps transactionally so concurrent callers cannot exceed them.
5. Manager UI edits tags lazily; do not load tag catalog on Quick Panel startup.
6. Failed migration must roll back the migration transaction; no down-migration is required because Ornare migrations are forward-only.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| Case/compatibility-equivalent duplicates | Exact NFKC + invariant uppercase normalized key plus UNIQUE constraint. |
| Unicode length bug | Count Unicode scalars (`EnumerateRunes()`), not UTF-16 code units. |
| Orphan junction rows | `PRAGMA foreign_keys=ON` on every connection + FK cascades. |
| Asset accidentally deleted with tag | Tag delete targets tag row; FK cascade deletes only `asset_tags`. |
| Concurrent cap race | Check/update inside transaction with DB uniqueness as final authority. |
| Startup regression | No tag repository query on Quick Panel startup. |

## Required tests

- Forward migration from the current production schema preserves every existing asset/history row.
- Injected migration failure rolls back transaction cleanly.
- NFKC/case/whitespace duplicate fixtures, including composed/decomposed Unicode.
- Unicode scalar length boundaries 1/40/41 and control-character rejection.
- Concurrent create/attach idempotency and cap enforcement.
- Delete tag removes junctions but leaves asset bytes/row.
- Foreign-key enforcement test on every Storage connection factory path.
- 10k asset/5k tag indexed-query benchmark.

## Performance requirements

- UNIQUE/index on `tags(normalized_name)`.
- Indexes on `asset_tags(asset_hash)` and `asset_tags(tag_id)` in addition to PK as query plan requires.
- No Quick Panel query/init.
- Manager uses paging/virtualization for large tag result sets.

## Approved sources / research

- **Microsoft.Data.Sqlite:** https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/
- **SQLite foreign keys:** https://sqlite.org/foreignkeys.html

Before copying source, re-check exact upstream license/source and record provenance according to `../01_OSS_SOURCES_AND_LICENSE_POLICY.md`.

## Forbidden implementations

- No JSON tag blob in asset row.
- No EF Core.
- No culture-dependent uniqueness.
- No eager global tag load at startup.

## Feature Definition of Done

- Forward migration and failed-migration transaction rollback fixtures green.
- Unicode/integrity/concurrency tests green.
- Large-library query benchmark green.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F15/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
