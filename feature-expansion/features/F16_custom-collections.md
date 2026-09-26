# F16 — Custom Collections

**Status:** IMPLEMENT  
**Dependencies:** F15,F18  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Allow users to organize the same canonical assets into multiple named collections without duplicating files and with deterministic low-write ordering.

## Fixed decisions

1. Collections are metadata only; an asset may belong to multiple collections.
2. Collection display name uses the **same exact F15 NFKC + invariant-uppercase normalization**, control rejection and Unicode-scalar counting; max display length is **80 scalars**.
3. Collection normalized names are case-insensitively unique; maximum **500 collections**.
4. Deleting a collection deletes only its junction rows; it never invokes asset cleanup.
5. Ordering uses signed 64-bit `sort_key` values spaced by **1024**.
6. New append item receives `lastSortKey + 1024` (first = `1024`). Insertion between neighbors uses integer midpoint when their gap is ≥2. If no integer gap exists, rebalance **only that one collection/order list** in one transaction to `1024,2048,...`; never rewrite unrelated collections.
7. V1 exposes **manual ordering for collections only**. Assets inside a collection use the existing Library sort/filter controls and F16 adds no `item_sort_key`, drag-reorder, or per-collection asset-order persistence.

## Integration points

- `Ornare.Storage`: `collections`, `collection_assets`, indexes/FKs.
- `Ornare.Core`: reuse F15 normalizer and collection commands.
- `Ornare.App`: Manager navigation/filter/reorder with virtualized views.

## Implementation sequence

1. Add forward migration with normalized unique name, timestamps and Int64 collection `sort_key`; the junction table references collection and canonical asset with `ON DELETE CASCADE` and contains no per-item sort column in V1.
2. Reuse exactly the F15 name normalizer; do not create a second normalization algorithm.
3. Implement transactional create/rename/delete/add/remove/reorder APIs.
4. Implement the 1024-gap midpoint/rebalance algorithm in one storage helper with overflow guard; if append would approach `Int64.MaxValue - 1024`, rebalance that list before append.
5. Manager loads collections lazily and pages/virtualizes collection contents.
6. Collection deletion performs no explicit asset-file deletion; normal global reference cleanup remains the only asset cleanup authority.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| Asset deletion through collection delete | FK cascade affects junction metadata only; collection API never calls asset cleanup. |
| Reorder write storm | Sparse 1024 keys; rebalance only the affected list when no midpoint exists. |
| Sort-key overflow | Detect before append/insertion and transactional local rebalance. |
| Name-normalization drift | Reuse F15 implementation and tests. |
| Huge collection UI | Paging/incremental loading + UI virtualization. |

## Required tests

- Migration/failed-migration rollback and exact F15 name normalization.
- Asset membership in multiple collections.
- Collection delete retains canonical asset and other memberships.
- Reorder: prepend/append/middle, repeated inserts until rebalance, and overflow-near fixture.
- Rebalance changes only the affected collection/list.
- 10k-item collection paging/scroll/query benchmark.
- Keyboard create/rename/delete/reorder flow.

## Performance requirements

- Paged indexed membership queries.
- Reorder normally updates one row; rebalance is rare and local.
- No collection initialization on Quick Panel startup.
- No duplicate asset bytes.

## Approved sources / research

- **Microsoft.Data.Sqlite:** https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/

## Forbidden implementations

- No filesystem folders for logical collections.
- No floating-point sort keys.
- No full-library materialization into UI.
- No separate name-normalization routine.

## Feature Definition of Done

- Schema/order/large-library tests green.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F16/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
