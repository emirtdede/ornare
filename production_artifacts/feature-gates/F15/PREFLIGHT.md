# Preflight Check: F15 — Library Tags

**Feature ID:** F15  
**Feature Name:** Library Tags  
**Execution Order:** 15 of 27  
**Traceability ID:** `ORN-EXP-F15`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Specification:** `feature-expansion/features/F15_library-tags.md`  
**Date:** 2026-09-26  

---

## 1. Scope & Objective

Add normalized many-to-many tags to Library assets with deterministic Unicode identity, database-enforced integrity, and no Quick Panel startup cost.
The tagging engine must:
1. Preserve user display casing while enforcing uniqueness through a deterministic invariant normalization algorithm.
2. Trim display names to 1–40 Unicode scalar values (`EnumerateRunes()`), rejecting empty strings and any Unicode control characters (`UnicodeCategory.Control`).
3. Enforce transactional limits: maximum 32 tags per asset and 5,000 tag rows in the entire library.
4. Guarantee referential integrity via SQLite foreign keys (`PRAGMA foreign_keys = ON;`) on every connection and cascade deletions on junction rows (`ON DELETE CASCADE`). Deleting a tag removes junctions only; it never deletes or damages the underlying asset.
5. Provide forward migration (`user_version = 3`) with transactional rollback safety upon failure.
6. Provide an allocation-conscious `LibraryNameNormalizer` in `Ornare.Core` reused across F15, F16 (Collections), and F17 (Search).
7. Implement flat tag display, editing, and tag filtering in Manager UI without loading tags during Quick Panel startup.

---

## 2. Invariants & Risk Mitigations

| Invariant / Risk | Specification Rule | Planned Mitigation |
|---|---|---|
| `F15-DEC-01` | User display casing preserved; uniqueness normalized | Store `display_name` and `normalized_name UNIQUE` in `tags` table. |
| `F15-DEC-02` | Display name length: 1–40 Unicode scalar values; no control characters | Use `EnumerateRunes()` to count scalars; reject `< 1` or `> 40` or `Rune.GetUnicodeCategory(r) == Control`. |
| `F15-DEC-03` | Max 32 tags per asset; max 5,000 total tags in library | Enforced transactionally in SQLite before insert to prevent race condition overshoots. |
| `F15-DEC-04` | Tag deletion removes tag relationships only; never deletes asset | `asset_tags` defines FK to `tags(id) ON DELETE CASCADE`. Delete tag only targets `tags` table. |
| `F15-DEC-05` | `PRAGMA foreign_keys = ON` on all Storage connections | Centralized open/connection builder enforces FKs; junction rows use FK cascade. |
| `F15-DEC-06` | Invariant normalization algorithm: Trim whitespace -> NFKC -> ToUpperInvariant -> NFKC -> reject control/empty | Implemented in `LibraryNameNormalizer.cs` (`Ornare.Core`). |
| `F15-DEC-07` | Migration V3 forward-only with rollback transaction | Injected failure rolls back migration cleanly, leaving database at version 2. |
| `F15-DEC-08` | Quick Panel startup zero cost | No tag repository queries during Quick Panel boot or icon apply. |

---

## 3. Test Strategy & Plan

1. **Normalization & Validation Unit Tests (`LibraryNameNormalizerTests` in `Ornare.Core.Tests`):**
   - Normalization idempotency across NFKC, uppercase, and trimming.
   - Accented characters, Turkish 'ı'/'i' culture-invariant consistency, and composed vs decomposed Unicode equivalents.
   - Unicode scalar counting boundary checks: 1 scalar, 40 scalars, 41 scalars (rejected). Multi-byte emojis counted as 1 scalar.
   - Rejection of control characters (`\u0000`, `\u0009`, `\u000A`, `\u001F`, `\u007F`, etc.).
2. **Storage Migration & Foreign Key Tests (`LibraryTagsStorageTests` in `Ornare.Storage.Tests`):**
   - Migration from V2 to V3 preserves existing assets, history, and render cache.
   - Injected failure during V3 rollback leaves schema at V2.
   - Connection factory verifies `PRAGMA foreign_keys = ON`.
   - Attempting to insert `asset_tags` with nonexistent `asset_hash` or `tag_id` throws FK constraint violation.
   - Deleting a tag deletes its junction rows in `asset_tags` without deleting the asset from `assets`.
   - Deleting an asset cascades to remove its rows from `asset_tags`.
   - Enforcing 32 tags per asset cap and 5,000 total tag cap.
   - Case-insensitive duplicate tag creation returns existing tag without duplication.
3. **Manager UI & Integration Tests:**
   - Tag addition and removal from asset detail/management view.
   - Tag filter filtering library items dynamically.
