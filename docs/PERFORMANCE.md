# Ornare - Performance Budgets

Performance is a product requirement, not an afterthought. These budgets are enforced by the
`Performance` test category and by manual measurement on the reference machine. A change that
exceeds a budget blocks release (`docs/QUALITY_GATES.md`, gate G5).

## Reference machine

- Windows 11 25H2, current servicing.
- 8 logical cores or better, SSD system drive.
- Measurements are taken with a quiet machine (no concurrent builds or indexing).

## Budgets

| Metric | Metric definition | Budget (P95) | Test |
|--------|-------------------|--------------|------|
| Explorer callback | `SHChangeNotify` handling per folder | ≤ 15 ms | `Phase11PersonalizationBenchmarkTests` |
| Template catalog lookup | 9-template descriptor lookup | ≤ 100 ms | `Phase11PersonalizationBenchmarkTests` |
| Icon search | Library search over the current library | ≤ 50 ms | `Phase11PersonalizationBenchmarkTests` |
| Cached preview | Re-render of a cached recipe | ≤ 25 ms | `Phase11PersonalizationBenchmarkTests` |
| Apply (internal pipeline) | Valid input through durable state, excluding process start | ≤ 500 ms | `Phase11PersonalizationBenchmarkTests` |
| CLI apply peak working set | Whole-process peak for `apply` | ≤ 180 MB | `Phase11PersonalizationBenchmarkTests` |

The CLI apply measurement deliberately excludes process start, runtime initialization and
native library load, which are one-off costs and not part of the apply work.

## Measurement rules

- Each metric is warmed up before samples are collected.
- P50 and P95 are reported; gates use P95.
- A gated run may take the best of several rounds to absorb scheduler noise, but a build that
  cannot meet the threshold in any round fails.
- Memory is sampled by polling the process working set; a run that exits before the first poll
  fails rather than reporting zero.

## Regression policy

- A performance change that touches apply, shell refresh, startup or rendering must record its
  measured impact in the pull request.
- A regression beyond budget requires either a fix or an accepted ADR that updates this
  document and the corresponding test threshold together.
