# Implementation Roadmap

This is sequence, not a timeline estimate.

## Gate 0 — Environment and dependency validation

- bootstrap solution/toolchain;
- verify the repository-pinned versions/licenses against authoritative sources; do not auto-upgrade pins;
- x64/ARM64 compile foundations;
- Microsoft Learn MCP active;
- package identity strategy verified.

## Gate 1 — Vertical architecture slice

- native shell command;
- app activation/single instance;
- minimal quick panel;
- one image type;
- real ICO;
- real relative desktop.ini apply;
- shell refresh;
- reset/undo;
- startup/shell benchmarks;
- Native AOT compatibility experiment.

Do not build polished manager before Gate 1 passes.

## Gate 2 — Reliability and secure image pipeline

- full allowed codecs;
- Magick policy;
- canonical library;
- SQLite baseline/history;
- durable operation journal + rollback/reconciliation;
- failure injection tests;
- cloud/removable caveat handling.

## Gate 3 — Premium design system and quick panel polish

- run `/design-freeze` before production XAML for signature surfaces;
- `VISUAL_REFERENCE_SPEC.md` + `UI_REVIEW_RUBRIC.md` gate;
- design tokens/components;
- deliberate XAML composition;
- drag states;
- Acrylic/fallback validation;
- keyboard/UIA;
- en-US/tr-TR;
- Light/Dark/High Contrast;
- screenshot reviews.

## Gate 4 — Manager

- Library;
- Recent;
- Favorites;
- Folders/history;
- Settings;
- Repair;
- diagnostics.

## Gate 5 — Packaging and broad qualification

- MSIX x64/ARM64;
- Windows 11 25H2 x64 + real ARM64 mandatory runtime matrix;
- applicable 26H1 real-hardware compatibility;
- WACK;
- install/update/uninstall;
- performance/security/license review.

## Gate 6 — Release candidate

No new features. Run the formal Gate 5/6 quality workflow, fix defects and validate acceptance criteria until zero P0/P1 defects remain and every release-blocking requirement has evidence.
