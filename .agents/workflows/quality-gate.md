---
description: Evaluate a named enterprise quality gate and refuse progression when evidence is missing.
---
# /quality-gate

Input: gate number/name.

1. Read `QUALITY_GATES.md` and all referenced specifications.
2. Enumerate required evidence before running anything.
3. Execute relevant build/test/audit/UI/performance/security checks.
4. Classify findings P0–P3; do not downgrade severity to pass.
5. Verify traceability for affected V1 requirements.
6. Produce `production_artifacts/quality/gate-<number>-report.md` with PASS/FAIL and exact evidence paths/commands/results.
7. If FAIL, provide a prioritized remediation plan and stop milestone progression until blockers are resolved.
