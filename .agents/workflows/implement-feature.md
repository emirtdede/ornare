---
description: Implement one V1 feature completely with research, tests, UI review and acceptance evidence.
---
# /implement-feature

Given a requested V1 feature:

1. map it to authoritative spec sections;
2. inspect existing code/tests;
3. list failure modes and invariants;
4. research version-sensitive APIs through MCP;
5. write/update behavior tests where practical;
6. implement production code directly in real source files;
7. build and run relevant tests;
8. if UI changed, run `/ui-review` logic and screenshots;
9. if performance/security/platform changed, run corresponding skill review;
10. update docs/ADR only for changed facts;
11. write `production_artifacts/quality/feature-<requirement-or-scope>-report.md` with concise implementation/test/review evidence.
