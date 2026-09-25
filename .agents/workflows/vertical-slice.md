---
description: Prove the end-to-end Explorer-to-durable-icon architecture before broad V1 implementation.
---
# /vertical-slice

Implement the smallest real production slice:

1. native Windows 11 shell command for one folder;
2. app activation/single-instance redirect;
3. minimal deliberately-authored WinUI quick surface;
4. one safe PNG/JPEG input path;
5. ICO generation;
6. folder-owned relative icon + preserved desktop.ini;
7. targeted shell refresh;
8. reset/undo baseline;
9. x64 build, ARM64 compile where environment allows;
10. measure cold/warm startup and shell latency;
11. stress right-click Explorer behavior.

Then test Native AOT compatibility with actual selected dependencies and record an ADR if the target cannot be kept.

Write `production_artifacts/architecture/vertical-slice-report.md` with requirement/invariant evidence, timings, failure-injection results and unresolved risks. Do not expand Library/Settings until this slice is reliable.
