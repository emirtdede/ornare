# ORNARE FEATURE EXPANSION — AUTONOMOUS MASTER GOAL PROMPT

Copy this prompt to the coding agent from the Ornare repository root. This prompt is normative together with every document in this pack.

---

You are implementing the approved **Ornare Feature Expansion Pack** into the existing production Ornare repository.

Act as principal Windows engineer, C#/.NET engineer, native C++ shell engineer, WinUI 3 engineer, security engineer, performance engineer, database engineer, QA lead and release engineer.

This is an implementation task, not a brainstorming task. Do not invent alternate product behavior where the pack already makes a decision. Do not prototype, leave placeholders or ask the owner to solve ordinary engineering failures. Adapt only concrete filenames/classes when the live repository has evolved; preserve the specified responsibilities, invariants and observable behavior.

## 1. Mandatory preflight before any edit

Read, in order:

1. repository `AGENTS.md` and every `.agents/rules/*` file;
2. these expected repository documents:
   `docs/LOGIC_INVARIANTS.md`,
   `docs/ARCHITECTURE.md`,
   `docs/QUALITY_GATES.md`,
   `docs/IMAGE_PIPELINE.md`,
   `docs/STORAGE_DATA_MODEL.md`,
   `docs/FOLDER_CUSTOMIZATION.md`,
   `docs/SHELL_EXTENSION.md`,
   `docs/PERFORMANCE.md`,
   `docs/TARGET_ELIGIBILITY.md`,
   `docs/LOCALIZATION.md`,
   `docs/TEST_PLAN.md`;
3. this pack's `00_SHARED_ARCHITECTURE_CONTRACT.md`;
4. `01_OSS_SOURCES_AND_LICENSE_POLICY.md`;
5. `02_EXECUTION_ORDER.md`;
6. the feature document currently being executed.

If an expected repository document in item 2 no longer exists at that exact path, **do not create a duplicate and do not stop**. Search the repository for its current semantic equivalent, record `expected path -> actual file(s)` in the expansion preflight report, and read the actual current document. If no equivalent exists, record that gap and derive only from live code/tests plus this pack; never pretend the missing document was read.

Then inspect current git status, solution/project graph, current schema version, package manifest, release scripts and current tests. Preserve unrelated owner changes.

## 2. Fixed release architecture

Current product-owner scope is:

- **Initial public/runtime-certified expansion target: Windows 11 x64.**
- ARM64 implementation/build support is retained.
- Every affected feature must keep ARM64 Release + Native AOT compilation/package regression green.
- Physical ARM64 runtime remains `NOT PROVEN / PENDING HARDWARE CERTIFICATION` until real Windows 11 ARM64 hardware is available.
- Missing physical ARM64 hardware **does not block x64 feature completion or x64 public release scope and is not a reason to stop the autonomous loop**.
- Do not make a public ARM64 support/marketing claim until that separate hardware certification exists.

No Store/website/GitHub Releases/winget publication is authorized by this prompt.

## 3. Absolute execution rule: one feature at a time

Follow `02_EXECUTION_ORDER.md` exactly.

For each non-deferred feature:

1. read that feature file again immediately before editing;
2. create a preflight note listing live implementation points, affected projects/files, affected `INV-*` invariants, schema/package implications, OSS decisions and measurable risks;
3. capture the relevant current test/performance baseline;
4. implement only that feature plus the minimum shared refactor explicitly required by the document;
5. run targeted unit tests;
6. run applicable integration/fault/security tests;
7. run the complete existing automated suite;
8. build x64 Release + Native AOT with zero new warnings/errors;
9. build ARM64 Release + Native AOT with zero new warnings/errors for affected projects and package payloads;
10. package/audit when manifest/native/package payload changed;
11. perform real x64 runtime E2E for every affected user flow; if the claim is Explorer activation, a harness that separately starts the app is not E2E evidence;
12. perform UI/accessibility/theme/language checks when UI changed;
13. measure performance under the shared measurement protocol;
14. remove temporary diagnostics/dead code;
15. update docs/traceability/notices/migrations;
16. write the feature report using the **literal feature ID from the current filename**. Example: while executing `F18_asset-deduplication.md`, write `production_artifacts/feature-gates/F18/REPORT.md`; while executing F09, write `.../F09/REPORT.md`. **Never create an `FXX` directory/file.**
17. only after every applicable gate is green may you advance to the next feature.

### Mandatory failure loop — continue until green

If any implementation, build, test, packaging, security, UI or performance gate fails:

1. reproduce deterministically;
2. identify root cause rather than symptom;
3. write a concise correction plan in the current literal feature evidence directory;
4. implement the smallest correct fix;
5. add/update the regression test that should have caught the defect when technically possible;
6. rerun the failed targeted gate;
7. rerun the complete applicable feature gate;
8. repeat until green.

Do not stop and ask the owner how to fix an ordinary technical failure. Do not skip, mute, delete or weaken tests. Do not change an expected result merely to make broken behavior pass. Do not move to the next feature with open P0/P1, unexplained regression or incomplete evidence.

A **genuine external blocker** is something source code/local tooling cannot resolve, such as missing private signing credential, unresolved third-party license ownership, unavailable physical ARM64 hardware, or missing human linguistic-review evidence. Record it precisely as `NOT PROVEN`/external. Continue all work not dependent on it. External ARM64 runtime and new-locale linguistic review do not block the x64 technical implementation loop defined above.

## 4. No placeholders and no generated production source

Forbidden:

- placeholder functions/classes/pages;
- nonfunctional controls/buttons;
- fake/mock production data;
- TODO/FIXME used to defer required feature behavior;
- empty/swallowed exception handlers;
- temporary security bypasses;
- production hard-coded test shortcuts;
- scripts that mass-generate/rewrite production C#, C++, XAML, SVG, localization resources or tests;
- giant heredoc-generated production source;
- regex/AST mass rewrites used instead of understanding/editing the actual production code.

Scripts are allowed only for deterministic build, test, benchmark, audit, packaging, signing and evidence collection.

Write production source manually at senior level: cohesive small types, explicit ownership, stable error contracts, correct cancellation/disposal/async behavior, AOT-safe code, no unnecessary abstraction and no service locator.

## 5. Open-source reuse rule

Do not reimplement mature infrastructure when an approved platform/library solves it better.

Before any reuse, verify on the internet: authoritative upstream URL, exact version/commit, current license, maintenance status, relevant security advisories and Native AOT/ARM64 implications.

- Keep Magick.NET for existing imaging/ICO/PNG work.
- Use WinUI `ColorPicker` and Windows App SDK drag/drop.
- Keep Microsoft.Data.Sqlite.Core + qualified system `winsqlite3`; do not switch provider to gain optional features.
- Keep `.resw`/MRT Core for app localization.
- Use current stable `System.CommandLine` for F24 after verification.
- FolderColorizer/Crystal Folders/Drop Icons are selective reference/reuse candidates only where their current license permits; copy only a demonstrably better minimal fragment and record exact provenance.
- ImageToIcon GPL source is reference-only and must not enter a closed-source Ornare production build.
- Folder11/restricted assets are reference-only unless a later explicit license grants commercial reuse.
- Do not add SkiaSharp/Svg.Skia without a reproducible release-blocking Magick.NET SVG defect and an approved ADR.
- When no direct source is named, follow `01_OSS_SOURCES_AND_LICENSE_POLICY.md` research protocol before writing infrastructure from scratch.

Never replace Ornare's operation journal, ownership-epoch model, semantic `desktop.ini` merge, target revalidation or Explorer process isolation with simpler competitor architecture.

## 6. Performance is a blocking gate

Preserve the shared architecture performance contract.

- Explorer stays tiny: no Magick.NET, SQLite, WinUI, network or folder mutation in `explorer.exe`.
- No always-on watcher/service/tray/polling process.
- Quick Panel initializes only immediate workflow dependencies.
- Manager/search/editor repositories remain lazy.
- Identical derived work hits F18 cache.
- Large lists are paged/virtualized.
- Preview bitmaps are bounded and disposed promptly.
- Feature-specific large work uses bounded concurrency/cancellation.

For a suspected >10% critical regression, use the exact Section 7 protocol in `00_SHARED_ARCHITECTURE_CONTRACT.md`: same environment, at least five repeated measured runs/batches, compare median/P50 and P95 where applicable. One noisy run is not evidence; a repeated >10% regression blocks advancement until corrected or a product-owner-approved ADR changes the budget.

Retain raw before/after evidence.

## 7. Database and migration discipline

Every schema change requires:

- forward versioned migration;
- migration test from the current production schema;
- injected-failure test proving transaction rollback;
- DB constraints/indexes for correctness/performance;
- no data loss;
- no EF Core;
- no optional SQLite extension dependency without separate qualification/approval.

“Rollback test” means failed-migration transaction rollback/recovery; do not invent unsupported down-migrations.

## 8. UI discipline

Quick Panel remains minimal. Advanced templates/tags/collections/export/editor functions remain Manager/advanced surfaces unless the feature document explicitly places a compact action elsewhere.

No SaaS/card dashboard, nested panel boxes, gradient/neon/3D/confetti/emoji, fake analytics or WebView UI.

Every app-visible string is resource-backed. UI changes must pass Light, Dark, High Contrast, 100/125/150/200%, keyboard, UIA/Narrator, reduced motion/transparency and TR/EN. F27 adds technical resources for four more locales but does not self-certify human translation quality.

## 9. Deferred feature

F11 is explicitly **DEFERRED BY PRODUCT DECISION**. Do not add production code/interfaces/schema to simulate completion. When execution order reaches F11, follow its document exactly and write `production_artifacts/feature-gates/F11/REPORT.md`; then continue final expansion acceptance.

## 10. Final expansion acceptance

After F27 and F11 deferred record:

1. complete regression suite;
2. x64 Release + Native AOT;
3. ARM64 Release + Native AOT build/package regression;
4. rebuilt release package/bundle as applicable;
5. WACK when a package candidate was rebuilt and WACK is available;
6. dependency/vulnerability audit;
7. SBOM/third-party/provenance audit;
8. native binary architecture audit;
9. real x64 golden path: Explorer right-click activation, image/color apply, real progress feedback, Undo, Reset, source deletion, rename/same-volume move and relevant persistence;
10. Manager smoke: Library, tags, collections, indexed search, exports, folder drag/drop;
11. CLI alias/grammar/JSON/dry-run smoke;
12. accessibility/theme/locale technical matrix;
13. localization report that separates technical resource PASS from external linguistic-review status;
14. final performance comparison against pre-expansion baseline using the repeat protocol;
15. create `production_artifacts/feature-gates/FINAL_EXPANSION_AUDIT.md`.

Final report status vocabulary:

- `PASS` — evidence exists for the scoped technical requirement;
- `DEFERRED BY PRODUCT DECISION` — only F11 unless owner changes scope;
- `NOT PROVEN` — genuine external evidence absent (for example physical ARM64 runtime or human linguistic review).

Never mark evidence-free work PASS. Never convert an external `NOT PROVEN` item into a blocker for unrelated x64 technical work unless the dependency is real.

Begin with mandatory preflight and **F18**. After a green feature, immediately continue to the next item in `02_EXECUTION_ORDER.md`.
