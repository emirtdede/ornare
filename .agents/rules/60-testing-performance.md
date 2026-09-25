---
description: Mandatory validation and measurable performance gates.
---
# Testing & Performance

Follow `docs/QUALITY_GATES.md`, `docs/FAULT_INJECTION_TESTS.md`, and the measured benchmark contract in `docs/PERFORMANCE.md`.

- Compile success is not feature completion.
- New domain behavior requires tests; bug fixes require regression tests when feasible.
- UI changes require `winapp ui`/UIA inspection plus screenshots and manual review.
- Explorer integration changes require repeated context-menu stress testing.
- Measure startup and apply flow; don't assume framework performance.
- No unnecessary background process/CPU.
- Profile regressions with WPR/WPA when needed.
- Test Windows 11 25H2 on x64 + real ARM64 before release; qualify 26H1 only on actual supported hardware/architecture per `PLATFORM_COMPATIBILITY.md`.
- Run WACK on release candidates.
