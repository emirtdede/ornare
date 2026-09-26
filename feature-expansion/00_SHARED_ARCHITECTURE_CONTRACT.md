# Ornare Feature Expansion — Shared Architecture Contract

**Status:** Normative. Every feature document in this pack depends on this contract.
**Target:** Current Ornare codebase: C#/.NET 10 LTS, WinUI 3, Windows App SDK 2.5.1 baseline, Native AOT release target, native C++ `IExplorerCommand` shell extension, Magick.NET, Microsoft.Data.Sqlite.Core + Windows system `winsqlite3.dll`, MSIX.

## 1. Non-negotiable process boundary

`Ornare.ShellExtension.dll` is the only Ornare component loaded into `explorer.exe`.

Explorer-side code may only:

1. validate that the shell selection shape is acceptable enough to expose a command;
2. obtain the selected filesystem path and cursor/context coordinates when needed;
3. create a tiny immutable activation payload;
4. activate/redirect to the packaged Ornare app;
5. return promptly.

Explorer-side code must **never** initialize or call Magick.NET, SQLite, image decoding, icon generation, history/undo storage, library search, network, WinUI, manager pages, diagnostics scans or filesystem mutation logic.

## 2. Project ownership

Use the existing solution boundaries; do not create a parallel architecture.

- `Ornare.Core`: domain commands, immutable requests/results, validation contracts, operation planning, error codes.
- `Ornare.Imaging`: secure image decode, SVG semantic recoloring, palette generation, placement, rasterization, ICO/PNG encoding, render cache-key logic.
- `Ornare.Storage`: SQLite migrations/repositories, content-addressed asset metadata, tags, collections, search metadata, operation/history metadata.
- `Ornare.Windows`: folder eligibility/identity, `desktop.ini` semantic merge, attributes, shell refresh, filesystem capability detection.
- `Ornare.App`: WinUI presentation, activation routing, Quick Panel, Manager, settings, localization wiring.
- `Ornare.DesignSystem`: reusable visual tokens/controls only; no business logic.
- `Ornare.ShellExtension`: tiny native C++ COM command surface only.
- `Ornare.Package`: MSIX manifest, package assets, AppExecutionAlias if/when CLI is integrated.

Do not move Windows-specific APIs into Core. Do not let App perform raw `desktop.ini` mutation. Do not let Storage perform image rendering.

## 3. Mutation architecture

Every change to a folder must pass through the existing durable operation engine and preserve these invariants:

- durable journal is written before the first user-visible filesystem mutation;
- target identity/configuration is revalidated immediately before commit;
- unrelated live `desktop.ini` semantics are preserved;
- reset/undo changes only semantics proven to be owned by Ornare;
- source-image lifetime is irrelevant after successful apply;
- same-folder concurrent mutation is serialized;
- external icon-semantic conflicts create an ownership-boundary conflict, not silent overwrite;
- cleanup is last and deletes only proven-unreferenced Ornare-owned files;
- shell refresh uncertainty does not roll back an otherwise durable correct mutation.

No feature may implement a second direct folder-mutation path.

## 4. Imaging architecture

Keep the current secure Magick.NET pipeline. Do not add SkiaSharp/Svg.Skia unless a reproducible, release-blocking SVG limitation is proven and an ADR demonstrates that the additional native dependency is worth the startup/package/security cost.

Rules:

- all external images are validated by the existing format/resource policy;
- SVG input cannot fetch network or arbitrary external files;
- every ICO size is rendered independently from normalized source/recipe state;
- default image placement never distorts aspect ratio;
- preview rendering is bounded and may use lower resolution; final ICO rendering occurs only when needed;
- derived render outputs use deterministic cache keys.

## 5. Data/storage architecture

Keep Windows system SQLite. Do **not** switch to bundled `e_sqlite3` merely to obtain optional extensions.

- all schema changes are versioned forward migrations;
- migrations are transactional and idempotent where practical;
- unique constraints enforce correctness, not only application checks;
- no duplicate image bytes or derived render blobs when a content/render hash already exists;
- no SQLite access from Explorer.

## 6. UI/product architecture

Quick Panel remains the golden-path surface: **folder → image/color action → processing → done**.

Do not turn it into a dashboard. Advanced functionality belongs in Manager/Editor surfaces. No nested cards, no card-in-card layout, no emoji, no fake metrics, no oversized branding, no WebView implementation.

Every new UI control must support:

- Light/Dark/System;
- High Contrast;
- keyboard and visible focus;
- UI Automation/Narrator names;
- 100/125/150/200% scaling;
- at least 40% localization expansion;
- Turkish and English resources with no production-visible hard-coded strings.

## 7. Performance contract

Before the first feature, capture the current baseline on the same physical x64 machine, power mode, OS session class, Release/Native-AOT configuration and test dataset that will be used for feature comparisons. Every feature gate compares against both that baseline and the immediately preceding certified feature.

Critical metrics include at minimum: Explorer menu callback latency, cold/warm Quick Panel activation, representative apply latency, Quick Panel working set, background CPU while idle, and any feature-specific query/render/CLI metric introduced later.

Measurement protocol:

1. warm up where the metric is defined as warm; cold metrics use a freshly terminated app/process and the existing documented cold-start procedure;
2. collect at least **20 measured runs/batches** after warm-up for critical startup/apply/query metrics so P95 is meaningful; for long destructive/fault scenarios where 20 repetitions are impractical, collect at least 5 and report median plus every individual value instead of claiming P95;
3. report median/P50 and P95 only for datasets with at least 20 measurements;
4. a single noisy run never proves regression;
5. if a critical metric is >10% worse, repeat under the same conditions and profile it; the feature fails only when the repeated median/P50 or established P95 regression remains >10%, unless an existing stricter product threshold is already defined;
6. never “prove noise” by changing machine/power mode/test data between before/after runs.

Hard rules:

- Explorer callback work stays in the current sub-10 ms typical design envelope and must not regress materially;
- no always-running background service, watcher, tray process or polling loop is added;
- background CPU when Ornare is not active remains approximately zero;
- Quick Panel startup path must not initialize Manager-only repositories or heavy optional surfaces;
- a repeated >10% critical-metric regression blocks the feature until optimized back within the gate or a product-owner-approved ADR changes the budget;
- Manager lists use virtualization/incremental loading for potentially large data sets;
- image previews are bounded; large source bitmaps are disposed immediately after the operation stage that requires them;
- batch work, where introduced later, must use bounded concurrency rather than unbounded `Task.WhenAll`.

## 8. Dependency policy

Approved existing stack is preferred over new packages. One new production package is pre-approved for the CLI feature: `System.CommandLine`, after the agent re-verifies current stable version, MIT license, Native AOT/trimming compatibility and vulnerability status.

Source reuse rules:

1. verify repository ownership, current license and the exact commit before reuse;
2. record reused source file/commit/license in `THIRD-PARTY-NOTICES.md` and SBOM/provenance records;
3. copy the smallest audited fragment, never an entire competing app;
4. adapt naming/style/tests to Ornare; do not preserve alien architecture;
5. GPL/personal-use/unclear-license source/assets are reference-only and must not be copied into a closed-source commercial build.

## 9. Coding rules

- No placeholder implementation.
- No TODO/FIXME used to defer required behavior.
- No fake production data.
- No catch-and-ignore exception blocks.
- No regex/heredoc/script mass generation of production C#/C++/XAML/SVG/tests.
- Scripts are allowed only for deterministic build/test/audit/packaging operations.
- No new god class or service locator.
- No reflection-heavy convenience API that damages Native AOT.
- Prefer explicit immutable request/result models and stable domain error codes.

## 10. Per-feature gate and release architecture scope

Current product-owner release scope is **Windows 11 x64 first**.

- x64 is the public runtime-certification target for this expansion.
- ARM64 source/build support is retained: every affected feature must keep ARM64 Release + Native AOT compilation/package regression green with zero new warnings.
- Physical ARM64 runtime behavior remains `NOT PROVEN / PENDING HARDWARE CERTIFICATION` until real Windows 11 ARM64 hardware is available.
- Lack of ARM64 physical hardware **does not block the x64 public scope** and must not cause the agent to stop the feature loop.
- No public ARM64 support claim/package marketing is allowed until the separate physical certification is completed.

A feature is not complete for the x64 expansion until all applicable items pass:

1. pre-change baseline and affected invariants identified;
2. targeted unit tests pass;
3. integration tests pass;
4. fault/error-path tests pass where filesystem/storage is touched;
5. full existing automated suite passes;
6. x64 Release + Native AOT build passes with zero new warnings;
7. ARM64 Release + Native AOT build/package regression passes with zero new warnings when the affected projects/package are in scope; ARM64 runtime remains explicitly unproven without hardware;
8. package build/audit passes when package/manifest/binaries changed;
9. real x64 runtime smoke passes for the user-visible flow;
10. UI Automation + accessibility + Light/Dark/High Contrast/TR-EN checks pass when UI changed;
11. performance comparison follows Section 7 and shows no unacceptable repeated regression;
12. documentation, migration notes, notices and evidence are updated;
13. temporary diagnostics are removed;
14. zero open P0/P1 for the feature.

When a test fails, do not skip it or weaken it. Reproduce → identify root cause → write a concrete correction plan → fix → rerun targeted tests → rerun the complete gate. Repeat until green before moving to the next feature.
