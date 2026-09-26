# Preflight Check: F17 — Library Search

**Feature ID:** F17  
**Feature Name:** Library Search  
**Execution Order:** 17 of 27  
**Traceability ID:** `ORN-EXP-F17`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Specification:** `feature-expansion/features/F17_library-search.md`  
**Date:** 2026-09-26  

---

## 1. Scope & Objective

Provide fast local prefix search over asset file/display names, tags, and collection names using the qualified Windows system SQLite provider without FTS5 dependency, full table scans, or metadata rewrite storms.
The search subsystem must:
1. Run without external FTS5 dependency using standard index-backed relational prefix matching on the Windows SQLite stack (`Microsoft.Data.Sqlite`).
2. Maintain three entity-local token tables: `asset_search_tokens`, `tag_search_tokens`, and `collection_search_tokens`.
3. Reuse the exact F15 NFKC + invariant uppercase normalizer from `LibraryNameNormalizer`. Split on any Unicode scalar that is not a letter or decimal digit (`!Rune.IsLetterOrDigit`).
4. Cap each token at 64 Unicode scalars, cap each asset at 64 unique tokens, and coalesce duplicates per entity.
5. Provide multi-token AND semantics where each query token matches by prefix across asset names, tags, or collections.
6. Guarantee index-backed prefix matching using lexical ranges (`token >= @term AND token < @termNext`) without leading-wildcard scans.
7. Perform schema creation in $O(1)$ at migration time (Migration V5). Backfill is never executed inside the schema migration transaction.
8. Persist durable index state (`search_index_state`) supporting incremental, resumable batches of 500 source entities per transaction across phases (`Assets -> Tags -> Collections -> Ready`).
9. Show localized `Preparing search index…` in the Manager UI during index construction and return no partial results until marked Ready.
10. Debounce UI searches at 120 ms with cancellation of stale queries. Quick Panel startup cost is zero.

---

## 2. Invariants & Risk Mitigations

| Invariant / Risk | Specification Rule | Planned Mitigation |
|---|---|---|
| `F17-DEC-01` | No FTS5 dependency; system SQLite provider unchanged | Use indexed B-Tree token tables and relational UNION pipeline. |
| `F17-DEC-02` | Three entity-local token tables; no denormalization rewrite storm | `asset_search_tokens`, `tag_search_tokens`, `collection_search_tokens`. Renaming tag/coll rewrites only its own tokens. |
| `F17-DEC-03` | Tokenization reuses F15 NFKC + uppercase; non-alphanumeric delimiters | `LibrarySearchTokenizer` with `EnumerateRunes`, `!Rune.IsLetterOrDigit(r)` splits. |
| `F17-DEC-04` | Max 64 scalars per token, max 64 tokens per asset | Strict bounded token extraction and deduplication. |
| `F17-DEC-05` | Whitespace/punctuation token AND semantics; prefix matching | Branching UNION query grouping by `asset_hash` with `COUNT(DISTINCT term_ord) = N`. |
| `F17-DEC-06` | Index-backed prefix matching; zero leading wildcard scans | Range seeks `token >= @term AND token < @termNext` verified by `EXPLAIN QUERY PLAN`. |
| `F17-DEC-07` | Migration V5 is $O(1)$; no blocking backfill inside migration | Migration creates schema only; Manager-owned incremental runner backfills on demand. |
| `F17-DEC-08` | Durable 500-entity batching; no partial search results | `search_index_state` table with cursor/phase; search returns empty/disabled until Ready. |
| `F17-DEC-09` | 120 ms debounce + cancellation; zero Quick Panel cost | Async cancellable search in UI; Quick Panel loads no search components. |

---

## 3. Test Strategy & Plan

1. **Tokenizer & Normalization Unit Tests (`LibrarySearchTokenizerTests` in `Ornare.Core.Tests`):**
   - Alphanumeric token extraction, non-alphanumeric splitting.
   - 64-scalar per-token truncation / validation.
   - 64 tokens per asset cap and duplicate coalescing.
   - NFKC and Turkish invariant consistency.
   - Lexical range boundary calculation (`termNext`).
2. **Storage Migration & Query Plan Tests (`LibrarySearchStorageTests` in `Ornare.Storage.Tests`):**
   - Migration V4 to V5 schema creation ($O(1)$).
   - Injected failure rollback leaves schema at V4.
   - `EXPLAIN QUERY PLAN` verification confirming `SEARCH ... USING INDEX` (zero `SCAN TABLE`).
   - Incremental 500-batch indexing and durable interrupted/resumed progress.
   - Multi-token AND query combining asset names, tags, and collections.
   - Tag and collection renames rewrite only that entity's tokens.
   - Cascade delete verification when assets, tags, or collections are removed.
   - 10k-asset and 50k-token search benchmark (< 50 ms P95).
3. **Manager UI Search Integration:**
   - 120 ms debounced search input with `SearchTextBox`.
   - Index preparing status banner / placeholder.
   - Dynamic search results display and clearing to default library view.
