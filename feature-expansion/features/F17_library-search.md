# F17 — Library Search

**Status:** IMPLEMENT  
**Dependencies:** F15,F16  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Provide fast local prefix search over asset display names, tags and collection names using the qualified Windows system SQLite provider, without FTS5 dependency, full scans or metadata rewrite storms.

## Fixed decisions

1. FTS5 is not a production dependency in this feature; the current system `winsqlite3` stack remains unchanged.
2. Use three entity-local token tables: `asset_search_tokens(asset_hash, token)`, `tag_search_tokens(tag_id, token)`, and `collection_search_tokens(collection_id, token)`. Renaming one tag/collection rewrites only that entity's own token rows; token text is never denormalized into every member asset.
3. Search normalization reuses the F15 shared NFKC + invariant-uppercase normalizer. Tokenization then splits on any Unicode scalar that is not a letter or decimal digit; empty tokens are dropped.
4. Each token is limited to **64 Unicode scalars**, each asset display name indexes at most **64 tokens**, and duplicate tokens per asset are coalesced.
5. Query semantics are whitespace/punctuation-token **AND**. Each normalized query token matches by prefix. Empty query returns the current paged/filter view.
6. Prefix matching must use an index-backed query plan; `%term%`/leading-wildcard default search is forbidden.
7. Schema creation is O(1) at migration time. Existing-library token backfill is **not** performed as one blocking migration transaction.
8. Search index version/state is durable. Manager builds/rebuilds entity-local token rows incrementally in batches of **500 source entities per transaction** (assets, then tags, then collections) under one index version. Until all three entity sets are complete for that version, search UI shows localized `Preparing search index…` and returns no knowingly partial results.
9. Search remains local, async, cancellable and debounced at **120 ms**; no network/AI.

## Integration points

- `Ornare.Storage`: three token tables, search-index state/version, and indexed relational search query.
- `Ornare.Core`: shared normalizer/tokenizer and search request/result.
- `Ornare.App`: Manager search box, index-preparing state, cancellation, paged/virtualized results.

## Implementation sequence

1. Add forward migration that creates `asset_search_tokens(asset_hash,token)`, `tag_search_tokens(tag_id,token)`, `collection_search_tokens(collection_id,token)` with composite primary keys and token-leading indexes, plus durable `search_index_version`/phase/cursor/completion state; do not backfill existing entities inside the schema migration.
2. Implement tokenizer using F15 normalization, Unicode scalar iteration, alphanumeric token boundaries and caps above.
3. Maintain asset display-name token rows transactionally when display name changes; maintain one tag's token rows when that tag is created/renamed; maintain one collection's token rows when that collection is created/renamed.
4. Tag/collection membership changes update only junction tables, never token rows for member assets.
5. Implement on-demand **Manager-owned** index preparation in fixed phases `Assets -> Tags -> Collections -> Ready`, processing 500 source entities per transaction with a durable phase/cursor; no process/service continues after Manager closes.
6. During incomplete build, disable actual search results and show `Preparing search index…`; on completion atomically mark the current index version ready.
7. Implement one SQL search pipeline per query term that UNIONs candidate asset hashes from:
   - `asset_search_tokens` prefix match directly to asset hash;
   - `tag_search_tokens` prefix -> `asset_tags` membership -> asset hash;
   - `collection_search_tokens` prefix -> `collection_assets` membership -> asset hash.
   Carry a query-term ordinal through each branch, UNION DISTINCT `(asset_hash, term_ordinal)`, then group by asset hash and require `COUNT(DISTINCT term_ordinal) = queryTokenCount` to implement AND semantics.
8. Parameterize the prefix as an escaped normalized prefix plus `%`. Create the necessary index/collation arrangement and capture `EXPLAIN QUERY PLAN`; if the qualified system SQLite does not use an index for the chosen `LIKE` form, switch to an equivalent indexed lexical range query rather than shipping a scan.
9. Page ordered results; UI debounces 120 ms and cancels stale requests.
10. FTS5 may be benchmarked only as research; it cannot become required without a separate ADR and provider qualification.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| System SQLite optional-extension differences | No FTS5 dependency. |
| Collection/tag rename rewrite storm | Only that tag/collection's own token rows change; member asset token rows remain untouched. |
| Prefix query silently scans | `EXPLAIN QUERY PLAN` gate proves index use; leading wildcards forbidden. |
| Long first migration freezes app | Schema only in migration; 500-row on-demand Manager backfill with durable state. |
| Partial index returns false negatives | No search result exposure until current index version is complete. |
| Query storm | 120 ms debounce + cancellation + paging. |

## Required tests

- Name/tag/collection prefix search independently and combined.
- Multi-token AND behavior across mixed sources (e.g. one term from name, one from tag).
- F15 normalization consistency and Turkish dotted/dotless-I fixtures with explicitly documented invariant-normalizer behavior.
- Tag/collection rename rewrites only its own entity-local token rows and does not rewrite `asset_search_tokens` or unrelated entity rows.
- `EXPLAIN QUERY PLAN` proves index-backed prefix access on the qualified Windows SQLite build.
- Interrupted index backfill resumes idempotently from durable state.
- Search disabled/preparing during incomplete index, then complete results after ready transition.
- 10k and 50k synthetic metadata benchmark with warm DB cache.

## Performance requirements

- Typical 10k-asset query target P95 `<50 ms` on the feature-gate benchmark machine after warm DB cache; record actual P50/P95.
- Backfill batch size is fixed at 500 source entities per transaction for this release.
- No search/index initialization on Quick Panel startup.
- Manager UI thread performs no synchronous SQLite enumeration.

## Approved sources / research

- **Microsoft.Data.Sqlite custom versions:** https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/custom-versions
- **SQLite query planner:** https://sqlite.org/queryplanner.html

## Forbidden implementations

- No Elasticsearch/Lucene/server.
- No bundled SQLite solely for FTS5.
- No `%substring%` default scan.
- No denormalizing tag/collection names into every asset token row.
- No blocking all-library backfill during startup/schema migration.

## Feature Definition of Done

- Indexed query plan evidence captured from the qualified runtime.
- Interrupted/resumed backfill and metadata-rename tests green.
- Large-library benchmark green.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F17/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
