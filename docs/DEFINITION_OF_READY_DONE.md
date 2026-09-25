# Definition of Ready / Definition of Done

## Feature Definition of Ready

A feature may enter implementation only when all applicable items are true:

- requirement/user outcome is stated in one sentence;
- scope and explicit non-scope are known;
- affected requirement IDs are identified;
- success and failure states are specified;
- data ownership/migration impact is known;
- security/privacy impact is reviewed;
- localization/accessibility impact is known;
- UI state specification exists for user-visible changes;
- platform assumptions are verified against current official documentation when version-sensitive;
- test approach is defined;
- unresolved architecture questions are converted to an ADR/spike rather than guessed during implementation.

## Feature Definition of Done

A feature is done only when:

- implementation satisfies the authoritative spec;
- no placeholder/mock/fake production behavior remains;
- build is warning-free under project policy;
- unit/integration tests pass;
- a regression test exists for fixed defects where feasible;
- relevant UIA tests pass;
- Light/Dark/High Contrast and keyboard/accessibility checks are completed for UI changes;
- en-US/tr-TR resources are complete and reviewed;
- no new dependency is unapproved/unrecorded;
- performance impact is measured for hot/startup/shell paths when relevant;
- security failure modes and path/input handling are tested;
- docs/traceability/ADR are updated if behavior/decision changed;
- evidence is written to the task implementation report;
- no P0/P1 defect remains in the feature scope.

## Release Definition of Done

A release is complete only when every release gate in `QUALITY_GATES.md` and `RELEASE_DISTRIBUTION.md` is satisfied with retained evidence. A green build alone is never a release decision.
