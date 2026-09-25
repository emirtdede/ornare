# Antigravity Development Playbook

## 1. Why this repository is modular

Antigravity loads workspace rules continuously and skills/workflows by task. Do not duplicate the complete spec into every prompt. The agent must discover the authoritative domain documents before changing code.

## 2. Mandatory execution sequence

### Phase A — Bootstrap / Gate 0

Run `/bootstrap`, then `/quality-gate` for Gate 0.

Expected evidence:

- prerequisite/toolchain inventory;
- repository-pinned package versions revalidated against authoritative sources without implicit upgrades;
- solution/projects authored through normal supported project tooling (not script-generated product code/UI);
- x64/ARM64 compile baseline;
- lockfiles/dependency audit initialized;
- no unresolved spec contradictions.

### Phase B — Risk-first vertical slice / Gate 1

Run `/vertical-slice`.

Prove end-to-end:

`Explorer command → app activation/single instance → minimal native quick surface → PNG input → ICO → folder-owned relative customization → durable journal/reconciliation → shell refresh → undo/reset`.

Then run `/architecture-review`, `/security-review`, `/performance-baseline`, and Gate 1. **No Manager/feature expansion before Gate 1 passes.**

### Phase C — Design freeze / Gate 3 inputs

Run `/design-freeze` on the real Quick Panel implementation. Required evidence includes Light/Dark/High Contrast screenshots, Turkish expansion, DPI/mixed-monitor behavior, UIA/keyboard checks and `UI_REVIEW_RUBRIC.md` >=18/20 with no zero.

A written claim that UI is “premium” is not evidence.

### Phase D — V1 implementation

Use `/implement-feature` per traced requirement/capability. Keep changes small enough for meaningful code review, unit/integration tests and fault injection.

### Phase E — Continuous verification

Run `/verify` regularly. Use `/security-review`, `/architecture-review`, `/performance-baseline` and `/ui-review` whenever the relevant risk surface changes.

### Phase F — Release candidate / ship

Run `/release-candidate` only after V1 requirements are implemented and evidence is complete. `/ship-v1` must refuse release readiness while any gate is FAIL/NOT PROVEN, P0/P1 is open, brand clearance is incomplete, or signing/SBOM/WACK/device evidence is missing.

## 3. Agent behavior

The agent must:

- inspect before editing;
- map work to immutable requirement IDs;
- protect `LOGIC_INVARIANTS.md` and state-machine legality;
- prefer current authoritative sources for version-sensitive platform facts;
- use small coherent changes;
- keep visible defect/risk evidence;
- compile/test after substantive changes;
- screenshot user-visible changes;
- independently challenge risk-critical work in a fresh review pass;
- write evidence under `production_artifacts/` using templates.

The agent must not:

- ask for already-decided preferences;
- redesign product behavior for easier coding;
- add V1-out-of-scope features opportunistically;
- hide incomplete behavior behind TODO/mock/fake success;
- equate a passing script with a passed release gate;
- claim “zero bugs”, “perfect”, or “production-ready” without the prescribed evidence.

## 4. UI implementation method

Design from written hierarchy and `VISUAL_REFERENCE_SPEC.md`. Write real XAML/control templates deliberately. Never use code-generation scripts, Figma-to-XAML export, screenshot tracing, or a generic dashboard/component dump.

After implementing each surface:

1. run it on Windows;
2. capture Light/Dark and required accessibility screenshots;
3. inspect UIA tree;
4. compare against visual/design specs;
5. remove unnecessary borders/containers;
6. test Turkish/text expansion;
7. test keyboard/focus;
8. score with `UI_REVIEW_RUBRIC.md` and save `templates/UI_REVIEW_REPORT.md` evidence.

## 5. Evidence standard

Major milestones must record:

- exact commit/build;
- changed files;
- requirement IDs verified;
- test commands/results;
- screenshots/capture paths;
- device/OS/architecture for runtime claims;
- P50/P95 numbers for performance claims;
- known risks/defects;
- explicit NOT PROVEN items.

No unverified claim is silently converted to PASS.
