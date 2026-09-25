---
description: Final ship gate; refuse release-ready status unless all acceptance, quality, security and packaging criteria pass.
---
# /ship-v1

Read `docs/ACCEPTANCE_CRITERIA.md` and every release/security/test/performance spec.

Run or verify every P0 gate with current evidence. Any failed/unrun P0 means NOT READY. Fix what can be fixed, rerun evidence, and continue until either all gates pass or a concrete external blocker remains.

Final output must contain:

- exact version/commit;
- package artifacts;
- tested OS/architecture matrix;
- test/WACK/security/license outcomes;
- performance numbers;
- known non-blocking issues;
- final name-clearance status;
- READY only when evidence supports it.
