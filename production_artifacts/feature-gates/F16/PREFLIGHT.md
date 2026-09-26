# Preflight Check: F16 — Custom Collections

**Feature ID:** F16  
**Feature Name:** Custom Collections  
**Execution Order:** 16 of 27  
**Traceability ID:** `ORN-EXP-F16`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Specification:** `feature-expansion/features/F16_custom-collections.md`  
**Date:** 2026-09-26  

---

## 1. Scope & Objective

Enable users to organize canonical Library assets into multiple custom named collections with low-write deterministic ordering, referential cascade integrity, and zero duplicate asset files.
The collection subsystem must:
1. Treat collections as pure metadata (assets can belong to 0, 1, or multiple collections).
2. Reuse the exact F15 NFKC + invariant-uppercase normalization, control-character rejection, and Rune scalar counting from `LibraryNameNormalizer` with an 80-scalar limit for collection names.
3. Enforce case-insensitive unique collection names and a hard cap of 500 collections total.
4. Guarantee referential integrity via SQLite foreign keys (`PRAGMA foreign_keys = ON;`) on every connection. Deleting a collection deletes only its junction records in `collection_assets`; it never triggers asset cleanup or deletes underlying icon files.
5. Provide signed 64-bit sparse ordering spaced by 1024 (`sort_key`) with integer midpoint insertion and local single-collection rebalancing when gaps collapse (< 2) or overflow approaches `long.MaxValue`.
6. Provide forward migration (`user_version = 4`) with transactional rollback safety upon failure.
7. Integrate with Manager UI for creating, renaming, deleting, reordering collections, and managing asset memberships without loading collection data on Quick Panel startup.

---

## 2. Invariants & Risk Mitigations

| Invariant / Risk | Specification Rule | Planned Mitigation |
|---|---|---|
| `F16-DEC-01` | Collections are metadata only; asset may belong to multiple collections | Many-to-many junction table `collection_assets(collection_id, asset_hash)`. |
| `F16-DEC-02` | Name normalization identical to F15; max 80 Unicode scalar values | Reuse `LibraryNameNormalizer.TryValidateAndNormalize` with `maxScalars = 80`. |
| `F16-DEC-03` | Unique normalized name; maximum 500 collections | DB unique index on `normalized_name` + transactional COUNT cap check (500 max). |
| `F16-DEC-04` | Collection deletion deletes junction rows only; never deletes asset | `collection_assets` foreign key with `ON DELETE CASCADE`; no call to asset cleanup. |
| `F16-DEC-05` | 64-bit sort keys spaced by 1024; midpoint insertion | `sort_key` integer arithmetic; `gap = K_B - K_A`; midpoint `K_A + gap / 2`. |
| `F16-DEC-06` | Local rebalance when gap < 2 or overflow approaches long.MaxValue | Rebalances `collections` in a single transaction to `1024, 2048, 3072, ...`. |
| `F16-DEC-07` | Forward migration V4 with rollback transaction | Injected failure rolls back migration cleanly, leaving database at version 3. |
| `F16-DEC-08` | Quick Panel startup zero cost | No collection queries during Quick Panel boot or icon apply. |

---

## 3. Test Strategy & Plan

1. **Storage Migration & Ordering Tests (`CollectionStorageTests` in `Ornare.Storage.Tests`):**
   - Migration from V3 to V4 preserves existing assets, tags, and history.
   - Injected failure during V4 rollback leaves schema at V3.
   - Foreign-key cascade: Deleting a collection removes rows from `collection_assets` while assets in `assets` remain intact.
   - Foreign-key cascade: Deleting an asset removes rows from `collection_assets` while collection in `collections` remains intact.
   - Midpoint ordering: Prepend, append, insert in middle, repeated insertions until gap collapse forces rebalance to 1024-step spacing.
   - Overflow detection: Pre-rebalance when `sort_key` approaches `long.MaxValue`.
   - Enforce 500 collections maximum cap.
   - Case-insensitive duplicate name rejection.
   - Multi-collection asset membership: Adding one asset to multiple collections and verifying independence.
   - 10k-asset membership query benchmark completing in < 50 ms.
2. **Manager UI & Integration:**
   - Collection creation, renaming, and deletion in Manager navigation/tabs.
   - Moving collection up/down using manual reorder commands.
