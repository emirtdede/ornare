# Feature Gate Report: F16 — Custom Collections

**Feature ID:** F16  
**Feature Name:** Custom Collections  
**Specification:** `feature-expansion/features/F16_custom-collections.md`  
**Execution Order:** 16 of 27  
**Traceability ID:** `ORN-EXP-F16`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Completion Date:** 2026-09-26  

---

## 1. Executive Summary

Feature F16 implements Custom Collections in Ornare, enabling users to organize canonical Library assets into multiple custom named collections with low-write deterministic ordering, referential cascade integrity, and zero duplicate asset files.

Key architectural deliverables:
1. **Reused Normalization & 80-Scalar Limit:** Reused the exact F15 NFKC + invariant uppercase normalizer from `LibraryNameNormalizer` with an expanded 80-scalar limit for collection names (`MaxScalarsPerCollection = 80`), control character rejection, and hard cap of 500 collections total (`MaxTotalCollections = 500`).
2. **Schema Migration V4 & Forward-Only Rollback Safety:** Added Migration V4 (`PRAGMA user_version = 4`) creating `collections` and `collection_assets` tables with compound indexes on `(sort_key ASC)`, `(normalized_name)`, `(asset_hash)`, `(collection_id)`, and `(collection_id, created_utc DESC)`. Injected migration failure cleanly rolls back the transaction leaving database at schema version 3.
3. **Sparse 1024-Spaced Midpoint Ordering & Overflow Guard:**
   - Appending items receives `lastSortKey + 1024` (first item = 1024).
   - Insertion between two neighbor collections uses integer midpoint `K_prev + (gap / 2)` when gap $\ge 2$.
   - When gap $< 2$ or append approaches `long.MaxValue - 2048`, local single-list rebalance reassigns all collection keys in one local transaction to `1024, 2048, 3072, ...` before computing the midpoint. Never touches unrelated collections or tables.
4. **Referential Integrity & Cascades:** SQLite foreign keys (`PRAGMA foreign_keys = ON;`) guarantee that deleting a collection deletes only its junction records in `collection_assets`; it NEVER triggers asset cleanup or deletes underlying icon files. Deleting an asset removes junction records while leaving collection metadata intact.
5. **Manager UI Virtualized / Paged Views:** Added `NavItemCollections` in navigation with custom split view: left panel for collection management (New, Rename, Delete, Move Up, Move Down) and right panel for virtualized gallery of collection assets with right-click "Remove from Collection", "Open in Explorer", etc. Added "Manage Collections..." dialog to asset context menus in Library and Favorites views.
6. **Zero Quick Panel Startup Cost:** Quick Panel does not query, load, or initialize collection tables.

---

## 2. Invariants & Gates Verification

| Invariant / Decision | Description | Verification Evidence | Status |
|---|---|---|---|
| `F16-DEC-01` | Collections are metadata only; asset may belong to multiple collections | Verified in `MultiCollectionAssetMembershipAndCascadeIntegrity`. | **PASS** |
| `F16-DEC-02` | Name normalization identical to F15; max 80 Unicode scalar values | Verified in `CollectionScalarLengthBoundariesExactly80Allowed81Rejected` and `NameNormalizationRulesAndDuplicateRejection`. | **PASS** |
| `F16-DEC-03` | Unique normalized name; maximum 500 collections | Verified in `NameNormalizationRulesAndDuplicateRejection` and `TotalCollectionsHardCapOf500`. | **PASS** |
| `F16-DEC-04` | Collection deletion deletes junction rows only; never deletes asset | Verified in `MultiCollectionAssetMembershipAndCascadeIntegrity`. | **PASS** |
| `F16-DEC-05` | 64-bit sort keys spaced by 1024; midpoint insertion | Verified in `SparseMidpointOrderingPrependAppendMiddleAndRebalance`. | **PASS** |
| `F16-DEC-06` | Local rebalance when gap < 2 or overflow approaches long.MaxValue | Verified in `SparseMidpointOrderingPrependAppendMiddleAndRebalance` and `OverflowGuardRebalancesBeforeAppend`. | **PASS** |
| `F16-DEC-07` | Forward migration V4 with rollback transaction | Verified in `ForwardMigrationV4PreservesExistingDataAndReachesVersion4` and `InjectedMigrationV4FailureRollsBackCleanlyToVersion3`. | **PASS** |
| `F16-DEC-08` | Quick Panel startup zero cost | Verified: Quick Panel loads no collection models, does no collection repository queries. | **PASS** |

---

## 3. Implementation Details

### A. Core Models & Normalizer (`src/Ornare.Core/`)
- Updated `LibraryNameNormalizer.cs`:
  - `MaxScalarsPerCollection = 80`.
  - `MaxTotalCollections = 500`.
  - Added `maxScalars` parameter to `TryValidateAndNormalize`.
- Created `LibraryCollection.cs`:
  - `LibraryCollection`: `Id`, `DisplayName`, `NormalizedName`, `SortKey`, `CreatedUtc`, `UpdatedUtc`.

### B. Storage Service & Migration V4 (`src/Ornare.Storage/`)
- Updated `IStorageService.cs` and `SqliteStorageService.cs`:
  - Added Migration V4 creating `collections` and `collection_assets` with foreign keys and cascade deletions.
  - Implemented `CreateCollectionAsync`, `GetCollectionByIdAsync`, `GetCollectionByNormalizedNameAsync`, `GetAllCollectionsAsync`, `RenameCollectionAsync`, `DeleteCollectionAsync`, `AddAssetToCollectionAsync`, `RemoveAssetFromCollectionAsync`, `GetAssetsInCollectionAsync`, `GetCollectionsForAssetAsync`, `ReorderCollectionAsync`, `RebalanceCollectionSortKeysAsync`, and `RebalanceInternalAsync`.

### C. Manager UI & Localization (`src/Ornare.App/`)
- Updated `ManagerWindow.xaml`:
  - Added `NavItemCollections` in navigation menu.
  - Added `CollectionsView` with split view: collection management list (with Move Up, Move Down, Rename, Delete) and collection asset gallery.
  - Added `ContextManageCollectionsText` in Library and Favorites context menus.
- Updated `ManagerWindow.xaml.cs`:
  - Added `CollectionItemVm` bindable view model.
  - Implemented `LoadCollectionsViewAsync`, `LoadCollectionAssetsAsync`, `OnCollectionsListViewSelectionChanged`, `OnNewCollectionClicked`, `OnRenameCollectionClicked`, `OnDeleteCollectionClicked`, `OnMoveCollectionUpClicked`, `OnMoveCollectionDownClicked`, `OnRemoveFromCurrentCollectionClicked`, and `OnManageCollectionsClicked`.
- Updated `Resources.resw` (`en-US` and `tr-TR`):
  - Added all collection UI strings, dialogs, button tooltips, and confirmation prompts.

---

## 4. Test & Verification Evidence

- **Core Normalization Unit Tests (`tests/Ornare.Core.Tests/`):** 45 tests PASS (including 80-scalar collection boundary tests).
- **Storage Unit & Integration Tests (`tests/Ornare.Storage.Tests/CollectionStorageTests.cs`):** All 40 storage tests PASS:
  - `ForwardMigrationV4PreservesExistingDataAndReachesVersion4`: PASS.
  - `InjectedMigrationV4FailureRollsBackCleanlyToVersion3`: PASS.
  - `NameNormalizationRulesAndDuplicateRejection`: PASS.
  - `TotalCollectionsHardCapOf500`: PASS.
  - `MultiCollectionAssetMembershipAndCascadeIntegrity`: PASS.
  - `SparseMidpointOrderingPrependAppendMiddleAndRebalance`: PASS.
  - `OverflowGuardRebalancesBeforeAppend`: PASS.
  - `TenThousandAssetMembershipQueryBenchmarkUnder50Ms`: PASS (indexed query completes in < 50 ms).
- **Solution-Wide Release Test Suite:** 321 tests PASS, 0 failures, 0 skipped:
  - `Ornare.Core.Tests`: 45 passed
  - `Ornare.UiTests`: 9 passed
  - `Ornare.Storage.Tests`: 40 passed
  - `Ornare.IntegrationTests`: 49 passed
  - `Ornare.Imaging.Tests`: 178 passed
- **ARM64 Managed Release Build (`scripts/build-managed-arm64.ps1`):** 6/6 projects build clean (0 errors, 0 warnings).
- **End-to-End Real Golden Path (`scripts/test-real-golden-path.ps1`):** 8/8 tests PASS, live `desktop.ini` preserved, 0 explorer restarts.

---

## 5. Traceability Compliance

- Requirement `ORN-EXP-F16` registered in `docs/REQUIREMENTS_TRACEABILITY.md`.
- Zero placeholders; zero script-generated code; zero breaking changes.
- Ready for local commit and progression to Feature 17 (F17 — Library Search).
