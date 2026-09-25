# Quality Gates and Defect Policy

Quality gates are fail-closed. An agent may not declare a gate passed without evidence.

## Severity model

### P0 — Critical / stop-the-line
Examples: Explorer crash/hang attributable to Ornare, unrelated user-file deletion/corruption, security boundary bypass, package cannot install/launch, data-loss regression, malicious input leading to code execution or unsafe external access.

Policy: zero open. Blocks development progression when the defect invalidates the current architecture slice and always blocks release.

### P1 — High
Examples: core golden path broken, reset/undo can restore wrong state, icon disappears after supported rename/move, shell command intermittently fails, protected path triggers unsafe elevation, major accessibility blocker, common image format fails, significant privacy leak.

Policy: zero open for RC/release. Normally blocks the affected milestone.

### P2 — Medium
Examples: non-core UX defect with workaround, minor visual inconsistency, performance regression outside critical target but still usable, edge-case localization issue.

Policy: may ship only if explicitly triaged with rationale, owner and target version; total P2 debt must be reviewed at RC.

### P3 — Low
Cosmetic/polish/documentation issue with no material correctness/accessibility/security impact.

Policy: may be deferred with traceable issue.

## Gate 0 — Specification / environment

Pass evidence:

- supported OS/toolchain verified;
- package baselines revalidated;
- no unresolved contradictory requirements;
- brand remains an explicit pre-publication gate;
- Microsoft Learn MCP/official sources available for version-sensitive Windows decisions;
- exact .NET SDK restored from `global.json`.

## Gate 1 — Architecture vertical slice

Must prove on x64 and compile for ARM64:

- modern Explorer command;
- safe app activation/single instance;
- quick panel first frame;
- one image path -> ICO;
- durable relative folder icon;
- preservation/reset/undo;
- targeted shell refresh;
- no Explorer restart;
- shell latency measurement;
- filesystem failure injection;
- Native AOT experiment with real dependencies.

No Manager/feature expansion before Gate 1 passes.

## Gate 2 — Reliability/security

- image policy enforced;
- malformed/corrupt/oversized fixtures rejected safely;
- SVG external references/network access blocked;
- reparse/race cases tested;
- transaction journal/reconciliation tested with injected failures after each commit stage;
- baseline/reset merge behavior proven not to clobber unrelated later changes;
- dependency audit has no unresolved release-blocking vulnerabilities.

## Gate 3 — Visual/UI freeze

- Quick Panel matches `VISUAL_REFERENCE_SPEC.md` and `UI_REVIEW_RUBRIC.md`;
- no nested cards/panels;
- Light/Dark/High Contrast reviewed;
- 100/125/150/200% and mixed-DPI reviewed;
- Turkish expansion reviewed;
- keyboard/UIA/Narrator smoke reviewed;
- reduced motion/transparency behavior reviewed;
- screenshots/evidence retained.

## Gate 4 — Complete V1

- every V1 requirement is traced to implementation + verification;
- Manager/Library/Favorites/Folders/Settings meet specs;
- migrations and reconciliation pass;
- install/update/uninstall paths pass;
- performance budget has measured P50/P95 baselines on named benchmark machines.

## Gate 5 — Release candidate

- x64 + ARM64 runtime qualification on real supported x64/ARM64 hardware;
- full regression matrix;
- Explorer stress test;
- WACK;
- SBOM + license notices + native binary inventory;
- signed package candidate;
- zero P0/P1;
- security review and threat model delta review;
- brand/Store clearance completed before public listing.

## Gate 6 — Ship

- immutable release tag/commit;
- final artifacts hash/signature verified;
- release notes/privacy/support docs complete;
- Store package submitted from the exact approved artifact lineage;
- rollback/recovery procedure validated.
