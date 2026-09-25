---
description: Run the current project's comprehensive engineering verification without packaging a release.
---
# /verify

- restore/build Release;
- run unit/integration tests;
- run dependency vulnerability audit;
- run localization resource completeness check;
- run relevant UI Automation smoke tests;
- verify no forbidden script-generated product files/workflows were introduced;
- inspect git diff for secrets, placeholders, dead code and accidental assets;
- write `production_artifacts/quality/verification-report.md`; report failures first, then passes.
