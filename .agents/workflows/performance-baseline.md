---
description: Establish or compare repeatable performance baselines for shell/startup/apply/UI scenarios.
---
# /performance-baseline

1. Read `PERFORMANCE.md` and identify scenario + target metric.
2. Record benchmark machine/VM model, CPU, RAM, architecture, OS build, power mode, display scale, package mode and commit.
3. Warm environment according to the documented scenario, then collect enough samples for P50/P95.
4. Use WPR/WPA for shell/startup/I/O CPU root-cause work; use complementary profiler only where useful.
5. Do not combine measurements from different profiles into one regression series.
6. Compare against baseline and regression policy.
7. Record raw summary and conclusion in `production_artifacts/performance/performance-baseline-<scope>.md`.
