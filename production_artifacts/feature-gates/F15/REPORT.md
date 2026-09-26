# Feature Gate Report: F15 — Library Tags

**Feature ID:** F15  
**Feature Name:** Library Tags  
**Specification:** `feature-expansion/features/F15_library-tags.md`  
**Execution Order:** 15 of 27  
**Traceability ID:** `ORN-EXP-F15`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Completion Date:** 2026-09-26  

---

## 1. Executive Summary

Feature F15 adds normalized many-to-many tags to Library assets with deterministic Unicode identity, database-enforced referential integrity, and zero Quick Panel startup cost.

Key architectural deliverables:
1. **Deterministic Unicode Tag Normalization:** Implemented `LibraryNameNormalizer` in `Ornare.Core`. It trims Unicode whitespace using `System.Text.Rune` semantics without intermediate allocations, normalizes via Unicode **NFKC** (`NormalizationForm.FormKC`), applies invariant uppercase (`.ToUpperInvariant()`), normalizes to NFKC again, and strictly validates that display length is between 1 and 40 Unicode scalar values (`EnumerateRunes()`) without control characters (`UnicodeCategory.Control`).
2. **Schema Migration V3 & Forward-Only Rollback Safety:** Added Migration V3 (`PRAGMA user_version = 3`) introducing `tags` and `asset_tags` tables with foreign keys and cascade deletions. Injected migration failure cleanly rolls back the transaction.
3. **Database-Enforced Referential Integrity:** Every SQLite connection created by `SqliteStorageService` enforces foreign keys via `ForeignKeys = true` and `PRAGMA foreign_keys = ON;`. Deleting a tag deletes its junction records in `asset_tags` but leaves the asset completely intact. Deleting an asset automatically cleans its tag junctions via `ON DELETE CASCADE`.
4. **Strict Transactional Limits:** Enforces a maximum of 32 tags per asset and 5,000 total tags in the library within atomic transactions.
5. **Manager UI Flat Tag Editor & Filter:** Added a horizontal tag filter bar to the Manager Library gallery and a "Manage Tags..." context menu action with an intuitive `ContentDialog` for adding and removing tags. Tag catalogs are loaded lazily in the Manager only and never touch the Quick Panel startup flow.

---

## 2. Invariants & Gates Verification

| Invariant / Decision | Description | Verification Evidence | Status |
|---|---|---|---|
| `F15-DEC-01` | User display casing preserved; uniqueness normalized | Verified in `CreateOrGetTagPreservesDisplayCasingAndEnforcesNormalizedUniqueness`. | **PASS** |
| `F15-DEC-02` | Display name length 1–40 Unicode scalar values; control characters rejected | Verified in `ScalarLengthBoundariesExactly40Allowed41Rejected` and `ControlCharactersAreRejected`. | **PASS** |
| `F15-DEC-03` | Max 32 tags per asset; max 5000 total tags in library | Verified in `AttachTagToAssetIsIdempotentAndEnforces32Cap` and `TotalTagLimit5000Enforced`. | **PASS** |
| `F15-DEC-04` | Tag deletion removes tag relationships only; never deletes asset | Verified in `DeleteTagRemovesJunctionRowsWithoutDeletingAsset`. | **PASS** |
| `F15-DEC-05` | `PRAGMA foreign_keys = ON` on all Storage connections | Verified in `VerifyForeignKeysEnabledOnStorageConnection` and `ForeignKeyEnforcementRejectsOrphanJunction`. | **PASS** |
| `F15-DEC-06` | Invariant normalization: Trim whitespace -> NFKC -> ToUpperInvariant -> NFKC -> reject control/empty | Verified in `LibraryNameNormalizerTests` (NFKC equivalence, Turkish invariant, surrogate pairs). | **PASS** |
| `F15-DEC-07` | Migration V3 forward-only with rollback transaction | Verified in `InjectedMigrationV3FailureRollsBackCleanly` and `ForwardMigrationV3PreservesExistingRowsAndReachesVersion3`. | **PASS** |
| `F15-DEC-08` | Quick Panel startup zero cost | Verified: Quick Panel loads no tag models, does no tag repository queries. | **PASS** |

---

## 3. Implementation Details

### A. Core Models & Normalizer (`src/Ornare.Core/`)
- Created `LibraryTag.cs`:
  - `LibraryTag`: `Id`, `DisplayName`, `NormalizedName`, `CreatedUtc`.
- Created `LibraryNameNormalizer.cs`:
  - Implements `TryValidateAndNormalize` with exact 6-step normalization algorithm.
  - Implements allocation-free `TrimUnicodeWhitespace` using `Rune.DecodeFromUtf16` and `Rune.DecodeLastFromUtf16`.
  - Implements `CountUnicodeScalars` counting Runes for surrogate pairs / emoji support.

### B. Storage Service & Database Migration (`src/Ornare.Storage/`)
- Updated `IStorageService.cs` and `SqliteStorageService.cs`:
  - Added Migration V3 creating `tags` and `asset_tags` with indexes on `normalized_name`, `tag_id`, and `asset_hash`.
  - Enforced `ForeignKeys = true` in connection string and executed `PRAGMA foreign_keys = ON;` on all opened connections.
  - Implemented `CreateOrGetTagAsync`, `AttachTagToAssetAsync`, `DetachTagFromAssetAsync`, `DeleteTagAsync`, `GetTagsForAssetAsync`, `GetAllTagsAsync`, `GetAssetsByTagAsync`, `GetTagsForAssetsAsync`, and `VerifyForeignKeysEnabledAsync`.

### C. Manager UI & Localization (`src/Ornare.App/`)
- Updated `ManagerWindow.xaml`:
  - Added `TagFilterScrollViewer` and `TagFilterBar` pill selector.
  - Added `ContextManageTagsText` menu item in `LibraryGridView` tile context menu.
- Updated `ManagerWindow.xaml.cs`:
  - Implemented tag filter selection and asset filtering.
  - Implemented `OnManageTagsClicked` dialog with real-time tag chip removal and new tag validation.
- Updated `Resources.resw` (en-US and tr-TR):
  - Added localized strings for tag filter, manage tags dialog, placeholders, and error messages.

---

## 4. Test & Verification Evidence

- **Core Normalization Unit Tests (`tests/Ornare.Core.Tests/LibraryNameNormalizerTests.cs`):** All 44 tests PASS.
- **Storage Integration & Scale Tests (`tests/Ornare.Storage.Tests/LibraryTagsStorageTests.cs`):** All 32 tests PASS.
  - `VerifyForeignKeysEnabledOnStorageConnection`: PASS.
  - `ForeignKeyEnforcementRejectsOrphanJunction`: PASS.
  - `ForwardMigrationV3PreservesExistingRowsAndReachesVersion3`: PASS.
  - `InjectedMigrationV3FailureRollsBackCleanly`: PASS.
  - `CreateOrGetTagPreservesDisplayCasingAndEnforcesNormalizedUniqueness`: PASS.
  - `AttachTagToAssetIsIdempotentAndEnforces32Cap`: PASS.
  - `TotalTagLimit5000Enforced`: PASS.
  - `DeleteTagRemovesJunctionRowsWithoutDeletingAsset`: PASS.
  - `DeleteAssetCascadesToJunctionRowsWithoutDeletingTag`: PASS.
  - `BulkGetTagsForAssetsAndQueryByTagWorkAccurately`: PASS.
  - `IndexedTagQueryBenchmark10kAssets5kTags`: PASS (< 50 ms on 10k assets / 5k tags).

---

## 5. Traceability Compliance

- Requirement `ORN-EXP-F15` registered in `docs/REQUIREMENTS_TRACEABILITY.md`.
- Zero placeholders; zero script-generated code; zero breaking changes.
- Ready for local commit and progression to Feature 16 (F16 — Custom Collections).
