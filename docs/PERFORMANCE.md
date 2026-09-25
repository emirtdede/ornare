# Performance Specification

Performance is part of UX. Measure instead of assuming WinUI is fast.

## 1. Targets and measurement contract

Initial engineering targets:

- Explorer command state/visibility work: **P95 <= 10 ms** on the standard shell benchmark scenario, excluding OS scheduling noise documented by the harness.
- Warm quick-panel meaningful-interaction time: **P95 <= 150 ms** target.
- Cold quick-panel meaningful-interaction time: **P95 <= 400 ms** target.
- Common-image apply latency (after valid input to durable state): **P95 <= 500 ms** target for the defined benchmark corpus.
- Idle CPU: effectively zero after settling; no polling loop.
- No resident background process solely to improve launch unless measurement and an ADR justify it.
- Quick-panel private working set: investigate >120 MB steady-state target and all >10% regressions; do not trade correctness for the number.
- UI remains responsive at 60/120 Hz; no repeated avoidable long frames during documented interactions.

Before Gate 4, freeze a benchmark manifest containing exact machines/VMs, power mode, OS build, architecture, display scale, package mode, corpus and run count. Record **P50/P95** and sample count. Targets are engineering release goals, not public marketing guarantees.

## 2. Startup strategy

Before first panel interaction, do not:

- scan the library;
- run repair;
- decode images;
- initialize heavy Magick.NET paths unnecessarily;
- open manager pages;
- perform broad SQLite queries;
- perform network access.

Use compiled bindings (`x:Bind`) and minimal visual tree where appropriate.

## 3. Profiling

Use Windows Performance Recorder/Analyzer (WPR/WPA) for startup, UI responsiveness, CPU and I/O investigations. Use Visual Studio/.NET profiling as complementary tooling.

Record representative cold/warm launches and drag/apply flows before RC.

## 4. Native AOT gate

Benchmark Native AOT vs non-AOT release once the vertical slice includes real dependencies. Compare:

- cold/warm startup;
- package size;
- working set;
- compatibility;
- crash diagnostics/servicing.

Do not select AOT only from theory.

## 5. Image pipeline

Decode/render runs off the UI thread with cancellation where appropriate. UI progress/status marshaling must remain cheap. Limit parallelism; V1 processes one target image at a time in the quick flow.

## 6. Regression gate

After a stable baseline exists:

- >10% P95 regression on Explorer callback or quick-panel startup requires investigation;
- >15% P95 regression on non-shell interactive scenarios requires investigation;
- a regression may be accepted only with measured evidence, user-value rationale and an ADR/release note when it changes a frozen budget.

Do not compare numbers from materially different hardware/OS/power profiles as if they were one benchmark series.
