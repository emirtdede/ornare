---
name: performance-profiling
description: Use for startup, Explorer latency, image apply latency, memory/CPU, animations, regressions, Native AOT, WPR/WPA, or performance acceptance work.
---
# Performance Profiling Skill

Read `docs/PERFORMANCE.md`.

- Benchmark Release builds.
- Separate cold/warm startup.
- Inspect what runs before first interactive quick panel.
- Use WPR/WPA for system-level evidence.
- Measure shell extension independently.
- Compare Native AOT/non-AOT only with real dependency slice.
- Do not keep a background daemon merely to fake launch speed without approved ADR.
