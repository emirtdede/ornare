# V1 Requirements Traceability Matrix

Every V1 requirement receives an immutable ID. Implementation reports and tests reference these IDs.

| ID | Requirement | Authoritative spec | Verification |
|---|---|---|---|
| ORN-FUN-001 | Exactly one supported folder exposes `Set Folder Icon` in modern Windows 11 context menu | PRODUCT_SPEC, SHELL_EXTENSION | shell integration/UIA/manual |
| ORN-FUN-002 | Drop local image and apply without Apply button | PRODUCT_SPEC, SCREEN_SPECIFICATIONS | UIA + integration |
| ORN-FUN-003 | File picker input works | PRODUCT_SPEC | UIA/integration |
| ORN-FUN-004 | Clipboard bitmap/local image file paste works | IMAGE_PIPELINE | integration/UIA |
| ORN-FUN-005 | Source image may be removed after apply | FOLDER_CUSTOMIZATION | integration/golden path |
| ORN-FUN-006 | Supported local rename/move preserves icon | FOLDER_CUSTOMIZATION | integration/manual Explorer |
| ORN-FUN-007 | Reset restores pre-Ornare icon semantics without clobbering unrelated current settings | FOLDER_CUSTOMIZATION, MIGRATION_RECOVERY | integration/fault injection |
| ORN-FUN-008 | Undo restores previous successful Ornare state | PRODUCT_SPEC | integration/UIA |
| ORN-FUN-009 | Recent and Favorites reuse canonical assets | STORAGE_DATA_MODEL | unit/integration/UIA |
| ORN-FUN-010 | Manager lists tracked folders and health | INFORMATION_ARCHITECTURE | integration/UIA |
| ORN-REL-001 | Explorer is not restarted/killed in normal flow | ARCHITECTURE | code review/stress |
| ORN-REL-002 | Existing unrelated desktop.ini semantics survive operations | FOLDER_CUSTOMIZATION | parser/merge integration |
| ORN-REL-003 | Cross-store failure produces rollback or recoverable journal state | ARCHITECTURE, FAULT_INJECTION_TESTS | deterministic fault injection |
| ORN-REL-004 | App uninstall keep-customizations leaves applied icons functional | RELEASE_DISTRIBUTION | clean VM install/uninstall |
| ORN-REL-005 | Long paths are either proven-safe through the qualified implementation or rejected before mutation; no truncation/wrong-target write | LONG_PATH_COMPATIBILITY, PLATFORM_COMPATIBILITY | boundary/long-path qualification |
| ORN-REL-006 | At most one mutation owns a folder identity at a time; duplicate activations are serialized/coalesced deterministically | ARCHITECTURE, STATE_MACHINES | concurrency/integration stress |
| ORN-REL-007 | Unsupported target classes are rejected before mutation; V1 never silently follows a target reparse point or requests elevation | TARGET_ELIGIBILITY, SECURITY_PRIVACY | target eligibility/race integration tests |
| ORN-REL-008 | External icon changes invalidate uncontested ownership; automatic Reset/Repair never overwrites them, and explicit reapply captures a fresh baseline | FOLDER_CUSTOMIZATION, STATE_MACHINES | conflict integration/UI tests |
| ORN-SEC-001 | Standard-user operation; no hidden elevation | SECURITY_PRIVACY | integration/manual |
| ORN-SEC-002 | Malicious/corrupt/oversized image inputs are bounded/rejected | IMAGE_PIPELINE, THREAT_MODEL | security corpus |
| ORN-SEC-003 | SVG cannot access network/external resources | IMAGE_PIPELINE | security integration |
| ORN-SEC-004 | Shell DLL loads no UI/DB/imaging/network stack | ARCHITECTURE | binary/dependency inspection |
| ORN-SEC-005 | Reparse/path changes cannot redirect unsafe writes silently | SECURITY_PRIVACY, THREAT_MODEL | race/reparse tests |
| ORN-PRV-001 | No analytics/telemetry/image upload/account in V1 | PRODUCT_SPEC, LOGGING_DIAGNOSTICS | binary/network review |
| ORN-UX-001 | Quick Panel has one continuous surface and no nested cards | UX_UI_SPEC, VISUAL_REFERENCE_SPEC | screenshot rubric |
| ORN-UX-002 | System theme default; Light/Dark/High Contrast supported | DESIGN_SYSTEM | visual/UIA |
| ORN-UX-003 | en-US + tr-TR complete; system language default | LOCALIZATION | resource parity/manual |
| ORN-UX-004 | Keyboard/UIA/Narrator golden path is usable | ACCESSIBILITY | UIA/manual |
| ORN-PERF-001 | Shell callbacks meet measured latency budget without heavy work | PERFORMANCE, SHELL_EXTENSION | WPR/WPA |
| ORN-PERF-002 | Quick-panel startup and apply baselines are measured P50/P95 | PERFORMANCE | benchmark report |
| ORN-PKG-001 | x64 + ARM64 packaged Release builds and runtime qualification pass | RELEASE_DISTRIBUTION | clean machines/WACK |
| ORN-SC-001 | Locked/pinned dependencies, vulnerability audit, SBOM and notices | DEPENDENCY_SUPPLY_CHAIN | CI/release artifacts |
| ORN-EXP-F18 | Canonical source asset and versioned render recipe deduplication with verified render cache and rollback-safe migration | F18_asset-deduplication, STORAGE_DATA_MODEL, IMAGE_PIPELINE | unit/integration/benchmark tests |
| ORN-EXP-F09 | Safe SVG recoloring with semantic template roles and single-color user SVG detection under zero-allocation security preflight | F09_svg-recoloring, IMAGE_PIPELINE, SECURITY_PRIVACY | unit/security/benchmark tests |
| ORN-EXP-F10 | Deterministic 5-tone folder palette generation using OKLab/OKLCH color science, binary search gamut mapping, and WCAG non-text contrast | F10_adaptive-contrast, IMAGE_PIPELINE, DESIGN_SYSTEM | unit/property/contrast/benchmark tests |
| ORN-EXP-F01 | 12 curated built-in folder colors, original ornare-classic semantic vector template, render cache integration, and compact Quick Panel color swatches | F01_folder-colors, IMAGE_PIPELINE, UX_UI_SPEC | unit/integration/E2E tests |
| ORN-EXP-F02 | Arbitrary custom color selection using native WinUI 3 ColorPicker, 80 ms debounced preview, cancel rollback, and MRU 8-item user-settings recents | F02_custom-color-picker, UX_UI_SPEC, STORAGE_DATA_MODEL | unit/integration/E2E tests |
| ORN-EXP-F06 | Image placement modes (Contain default, Cover center-crop, Fill explicit stretch with distortion notice), allocation-free pure geometry calculator, 256px preview equivalence, deterministic cache key divergence | F06_contain-cover-fill, IMAGE_PIPELINE, UX_UI_SPEC | unit/integration/E2E tests |
| ORN-EXP-F07 | Controlled inset padding (0–30% per side, 8% default when enabled, 0% canonicalized to Off), midpoint rounding across all 10 ICO frame sizes, preview debounce without full ICO render, cache key integration | F07_transparent-padding, IMAGE_PIPELINE, UX_UI_SPEC | unit/integration/E2E tests |
| ORN-EXP-F08 | Solid background color compositing over transparent source assets, native WinUI 3 ColorPicker reuse, sRGB alpha-blending without fringe artifacts, canonical key divergence, strict asset immutability | F08_background-color, IMAGE_PIPELINE, UX_UI_SPEC | unit/integration/E2E tests |
| ORN-EXP-F03 | Three original Ornare folder style templates (Classic, Soft, Flat), shared proportions and semantic roles, lazy catalog loading (<1 ms), strict validator, multi-silhouette rendering, and QuickPanel selector | F03_folder-style-templates, IMAGE_PIPELINE, UX_UI_SPEC | unit/integration/E2E tests |
| ORN-EXP-F04 | Four semantic work-status presets (In Progress, Done, Archive, Important) using licensed MIT Fluent UI System Icons badges, contrast-adapted backing disk, small-frame badge omission, multi-template ICO rendering, and Manager Presets view | F04_status-presets, IMAGE_PIPELINE, INFORMATION_ARCHITECTURE | unit/integration/E2E tests |
| ORN-EXP-F05 | Three visual priority presets (High, Medium, Low) using Ornare-authored geometric badges, standard status preset compositor and omission rules, and localized organization disclaimer in Manager Presets view | F05_priority-presets, IMAGE_PIPELINE, INFORMATION_ARCHITECTURE | unit/integration/E2E tests |
| ORN-EXP-F12 | Safe ICO import with container validation (magic, limits, hostile corpus), fast-path preservation for compliant 10-frame icons, deterministic frame-selection normalization, bounded preview extraction, and Library import UI | F12_ico-import, IMAGE_PIPELINE, STORAGE_DATA_MODEL | unit/integration/E2E tests |
| ORN-EXP-F13 | Atomic ICO export with exact 10-frame compliance, verified F18 render-cache copy reuse, sibling temp ReplaceFileW/MoveFileExW semantics, and zero cancel mutation | F13_ico-export, STORAGE_DATA_MODEL, UX_UI_SPEC | unit/integration/E2E tests |
| ORN-EXP-F14 | Atomic PNG export with exact 256/512/1024 sizing, native vector rasterization scaling, transparent & solid background compositing, cache reuse, sibling temp ReplaceFileW semantics, and zero UI lockup | F14_png-export, IMAGE_PIPELINE, UX_UI_SPEC | unit/integration/E2E tests |
| ORN-EXP-F15 | Normalized many-to-many Library tags with deterministic Unicode NFKC identity, database-enforced foreign keys, transactional caps (32/asset, 5000 total), cascade integrity, and Manager-only flat tag editor/filter | F15_library-tags, STORAGE_DATA_MODEL, INFORMATION_ARCHITECTURE | unit/integration/benchmark tests |
| ORN-EXP-F16 | Custom collections with 1024-spaced sparse ordering, integer midpoint rebalance, referential cascade integrity, 80-scalar normalizer, and Manager collections view | F16_custom-collections, STORAGE_DATA_MODEL, INFORMATION_ARCHITECTURE | unit/integration/scale/benchmark tests |
| ORN-EXP-F17 | Local prefix library search with entity-local token tables, indexed lexical range scan, transactional sync, background backfill, 120 ms UI debounce, and cancellation token support | F17_library-search, STORAGE_DATA_MODEL, INFORMATION_ARCHITECTURE | unit/integration/benchmark tests |
| ORN-EXP-F19 | Portable folder mode with folder-owned relative icon semantics, stable NTFS identity (VolumeSerialNumber, FileId64), open-by-ID relocation reconciliation, migration V6, and zero universal portability claims | F19_portable-folder-mode, FOLDER_CUSTOMIZATION, STORAGE_DATA_MODEL, WINDOWS_SHELL | unit/integration/E2E tests |
| ORN-EXP-F20 | Folder drag and drop into Manager with Target Tray staging, zero disk or database mutation on drop, 1000 item limit, bounded concurrency, alias/NTFS identity coalescence, and bilingual localization | F20_folder-drag-and-drop-into-manager, UX_UI_SPEC, WINDOWS_SHELL, STORAGE_DATA_MODEL | unit/integration/E2E tests |


## Traceability rule

No requirement may be marked complete with only an implementation file reference. It needs at least one verification artifact/test. New V1 behavior receives a new ID before implementation.
