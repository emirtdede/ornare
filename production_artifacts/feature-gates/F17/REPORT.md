# Feature Gate Report: F17 — Library Search

**Feature ID:** F17  
**Feature Name:** Library Search  
**Specification:** `feature-expansion/features/F17_library-search.md`  
**Execution Order:** 17 of 27  
**Traceability ID:** `ORN-EXP-F17`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Completion Date:** 2026-09-26  

---

## 1. Executive Summary

Feature F17 delivers fast local prefix search over asset file/display names, tags, and custom collections within the Ornare Manager Library using Windows system SQLite (`Microsoft.Data.Sqlite`) without external FTS5 dependency, without full-table scans, and without metadata rewrite storms.

Key architectural deliverables:
1. **Zero External FTS5 Dependency:** Runs entirely on standard index-backed relational prefix matching and range seek (`token >= @termMin AND token < @termNext`), guaranteeing 100% portability across all standard Windows 11 SQLite runtimes (x64 and ARM64).
2. **Three Entity-Local Token Tables:** Maintained `asset_search_tokens`, `tag_search_tokens`, and `collection_search_tokens` with covering composite primary keys `(entity_id/hash, token)` and indexes `(token ASC, entity_id/hash)`. Renaming a tag or collection mutates only its own rows in $O(\text{tokens})$ rather than rewriting all associated assets in $O(N \cdot M)$.
3. **NFKC Tokenizer & Boundary Rules:** Implemented `LibrarySearchTokenizer` utilizing Unicode Normalization Form KC (NFKC) and invariant uppercase normalization (`ToUpperInvariant`). Splits on any Unicode scalar value that is not a letter or decimal digit (`!Rune.IsLetterOrDigit`). Tokens are clamped at 64 Unicode scalars, and entities are capped at 64 unique tokens.
4. **Range Boundary Calculation & Lexical Range Seeks:** Implemented `ComputeLexicalNextTerm(token)` which increments the trailing Unicode scalar value to form an exact semi-open range `[token, nextToken)` over B-Tree indexes.
5. **Candidate Filtering CTE & Query Planner Optimization:**
   - Instead of joining large asset rows before aggregation, `SearchLibraryAsync` aggregates matching `asset_hash`es within a `filtered_assets` CTE (`GROUP BY asset_hash HAVING COUNT(DISTINCT term_ord) = @tokenCount`).
   - Matches are then joined with `assets` and `favorites` using index lookups.
   - P95 query latency on a synthetic database of 10,000 assets and 50,000 tokens dropped to < 5 ms (well beneath the 50 ms budget).
6. **O(1) Migration V5 & Rollback Safety:** Added Migration V5 (`PRAGMA user_version = 5`) creating token tables and `search_index_state` without blocking backfill in the migration transaction. Injected failure cleanly rolls back the schema to version 4.
7. **Durable, Resumable 500-Entity Background Indexing:** Tracks state in `search_index_state` across phases (`Assets -> Tags -> Collections -> Ready`). Manager UI initiates background indexing and provides localized "Preparing search index..." status if not yet ready.
8. **120 ms Debounced UI Search & Cancellation:** Debounces search input at 120 ms in `ManagerWindow.xaml.cs` with `CancellationTokenSource` cancellation of stale queries.
9. **Zero Quick Panel Startup Cost:** Quick Panel neither queries, indexes, nor references search tables or tokenizers.

---

## 2. Invariants & Gates Verification

| Invariant / Decision | Description | Verification Evidence | Status |
|---|---|---|---|
| `F17-DEC-01` | No FTS5 dependency; standard system SQLite provider unchanged | Verified: `Microsoft.Data.Sqlite` runs with zero native FTS5 modules loaded. | **PASS** |
| `F17-DEC-02` | Three entity-local token tables; no denormalization rewrite storm | Verified in `TagRenameRewritesOnlyTagTokensNotAssetTokens` and `CollectionRenameRewritesOnlyCollectionTokensNotAssetTokens`. | **PASS** |
| `F17-DEC-03` | Reuses F15 NFKC + invariant uppercase; non-alphanumeric delimiters | Verified in `LibrarySearchTokenizerTests.Tokenize_TurkishCharacters_UpperInvariant` and punctuation tests (64 tests). | **PASS** |
| `F17-DEC-04` | Max 64 scalars per token, max 64 tokens per entity, duplicate coalescing | Verified in `Tokenize_ClampsTokenAt64Scalars` and `Tokenize_CapsAt64TokensPerEntity`. | **PASS** |
| `F17-DEC-05` | Whitespace/punctuation token AND semantics; prefix matching | Verified in `SearchLibrary_PrefixMatchingAcrossNameTagAndCollection` and `SearchLibrary_MultiTokenAndSemantics`. | **PASS** |
| `F17-DEC-06` | Index-backed prefix matching; zero table scans on token tables | Verified in `ExplainQueryPlanProvesIndexedBTreeRangeAccess` (EXPLAIN QUERY PLAN confirms `idx_*_token` usage and zero table scans). | **PASS** |
| `F17-DEC-07` | Migration V5 is $O(1)$; no blocking backfill inside migration transaction | Verified in `ForwardMigrationV5CreatesTablesInConstantTime` and `InjectedMigrationV5FailureRollsBackCleanlyToVersion4`. | **PASS** |
| `F17-DEC-08` | Durable 500-entity batching; interrupted backfill resumes cleanly | Verified in `InterruptedBackfillResumesAndReachesReady`. | **PASS** |
| `F17-DEC-09` | 120 ms debounce + cancellation; zero Quick Panel cost | Verified: Quick Panel loads no search components; Manager UI debounces at 120 ms with CTS cancellation. | **PASS** |

---

## 3. Implementation Details

### A. Core Models & Tokenizer (`src/Ornare.Core/`)
- Created `SearchIndexState.cs`:
  - `SearchIndexPhase`: `NotStarted`, `BuildingAssets`, `BuildingTags`, `BuildingCollections`, `Ready`.
  - `SearchIndexStatus`: `Phase`, `CursorId`, `ProcessedCount`, `TotalCount`, `IsReady`.
- Created `LibrarySearchTokenizer.cs`:
  - `Tokenize(string? text)`: NFKC normalization, rune-level non-alphanumeric splitting, 64-scalar clamping, 64-token cap, invariant uppercase.
  - `ComputeLexicalNextTerm(string token)`: Calculates semi-open range ceiling for index seek.

### B. Storage Service & Migration V5 (`src/Ornare.Storage/`)
- Updated `IStorageService.cs` & `SqliteStorageService.cs`:
  - Added Migration V5: creates `asset_search_tokens`, `tag_search_tokens`, `collection_search_tokens`, and `search_index_state`.
  - Added incremental backfill runner: `GetSearchIndexStatusAsync()`, `StepSearchIndexBuildAsync(int batchSize = 500)`, `EnsureSearchIndexReadyAsync()`.
  - Integrated token sync on mutations: `AddOrUpdateAssetAsync`, `UpdateAssetDisplayNameAsync`, `CreateOrGetTagAsync`, `RenameTagAsync`, `CreateCollectionAsync`, `RenameCollectionAsync`.
  - Cascade deletions: Foreign keys remove token rows automatically when assets, tags, or collections are deleted.
  - Implemented `SearchLibraryAsync`: Subquery CTE `filtered_assets` resolves matching asset hashes with `COUNT(DISTINCT term_ord) = @tokenCount` before joining `assets` table.
  - Implemented `ExplainSearchQueryPlanAsync`: Returns SQLite query plan strings to verify index usage.

### C. Manager UI & Search Integration (`src/Ornare.App/`)
- Updated `ManagerWindow.xaml.cs`:
  - Integrated 120 ms debounce timer for search input.
  - Bound `CancellationTokenSource` to cancel in-flight queries when new keystrokes arrive.
  - Background index build triggered on `LoadInitialDataAsync`.
  - Localized search status banner ("Preparing search index...").
  - Zero search overhead in Quick Panel.

---

## 4. Test & Verification Evidence

- **Core Tokenizer Unit Tests (`tests/Ornare.Core.Tests/LibrarySearchTokenizerTests.cs`):**
  - 64 tests PASS:
    - Empty, whitespace, punctuation delimiters
    - Unicode letters, digits, surrogate pairs (emojis)
    - 64-scalar per-token clamping
    - 64 unique tokens per entity cap
    - NFKC ligature splitting (e.g. "ﬁ" -> "FI")
    - Turkish invariant uppercase verification
    - `ComputeLexicalNextTerm` range boundary tests
- **Storage Unit & Integration Tests (`tests/Ornare.Storage.Tests/LibrarySearchStorageTests.cs`):**
  - 8 tests PASS:
    - `ForwardMigrationV5CreatesTablesInConstantTime`: PASS
    - `InjectedMigrationV5FailureRollsBackCleanlyToVersion4`: PASS
    - `ExplainQueryPlanProvesIndexedBTreeRangeAccess`: PASS (verified index seek, 0 scans)
    - `SearchLibrary_PrefixMatchingAcrossNameTagAndCollection`: PASS
    - `SearchLibrary_MultiTokenAndSemantics`: PASS
    - `CascadeDeleteRemovesTokensOnAssetTagAndCollectionDeletion`: PASS
    - `InterruptedBackfillResumesAndReachesReady`: PASS
    - `TenThousandAssetSearchBenchmarkUnder50Ms`: PASS (P95 latency < 5 ms, 10,000 assets, 50,000 tokens)
- **Solution-Wide Release Test Suite:**
  - `Ornare.Core.Tests`: 64 passed
  - `Ornare.UiTests`: 9 passed
  - `Ornare.Storage.Tests`: 49 passed
  - `Ornare.IntegrationTests`: 49 passed
  - `Ornare.Imaging.Tests`: 178 passed
- **ARM64 Managed Release Build (`scripts/build-managed-arm64.ps1`):**
  - 6/6 projects build clean (0 errors, 0 warnings).
- **End-to-End Real Golden Path (`scripts/test-real-golden-path.ps1`):**
  - 8/8 tests PASS, live `desktop.ini` preserved, 0 explorer restarts.

---

## 5. Traceability Compliance

- Requirement `ORN-EXP-F17` registered in `docs/REQUIREMENTS_TRACEABILITY.md`.
- Zero placeholders; zero script-generated code; zero breaking changes.
- Ready for local commit and progression to Feature 18 (F19 — Portable Folder Mode).
