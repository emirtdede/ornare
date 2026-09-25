---
description: Validate a complete Ornare V1 build against release gates and produce an evidence report.
---
# /release-candidate

1. Freeze scope; no feature work.
2. Build x64/ARM64 Release packages; run mandatory 25H2 x64/ARM64 qualification and applicable 26H1 compatibility qualification.
3. Run full automated suites.
4. Run golden-path/manual OS/DPI/theme/language matrix.
5. Run Explorer stress/crash checks.
6. Run startup/apply performance captures and compare budget.
7. Run security/dependency/license audits; generate SBOM/native inventory.
8. Run WACK.
9. Test clean install, update, uninstall keep-customizations and restore-all if implemented.
10. Verify privacy/no-network behavior.
11. Verify third-party notices.
12. Verify no Apple proprietary assets.
13. Verify name-clearance gate status.
14. Run `/quality-gate` for Gate 5 and confirm zero P0/P1.
15. Produce `production_artifacts/release/release-candidate-report.md` with PASS/FAIL evidence. Do not use percentages or vague “looks good”.
