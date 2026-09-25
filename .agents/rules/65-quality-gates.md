---
description: Fail-closed quality gates, defect severity and evidence rules.
---
# Quality Gates

- `docs/QUALITY_GATES.md` is authoritative for milestone/release progression.
- Zero open P0/P1 defects for release candidate approval.
- Never claim a gate passed without commands, tests, screenshots, benchmark data or other retained evidence appropriate to that gate.
- UI changes must pass `docs/UI_REVIEW_RUBRIC.md`; subjective “looks good” is not enough.
- Explorer crash/hang, unrelated data corruption, or unsafe path mutation is P0.
- Do not hide failing tests, reduce coverage, relax security, or delete checks to make a gate green.
