# Test Plan

Testing is risk-based and traceable to `REQUIREMENTS_TRACEABILITY.md`.

## 1. Test layers

### Unit

- image normalization/policy decisions;
- content hash + pipeline-version behavior;
- desktop.ini icon-semantic read/merge/conflict logic;
- domain validation/state machines;
- error mapping/retryability;
- localization resource coverage helpers;
- DB migration/reconciliation pure logic.

### Integration

- apply/reset/undo on disposable local folders;
- preservation of unrelated desktop.ini settings, including settings changed **after** initial Ornare apply;
- folder-owned ICO persistence;
- SQLite + filesystem durable operation journal/reconciliation;
- rename/move scenarios;
- permission/disk/full-target failures;
- long-path qualification;
- reparse/junction/path-race cases;
- crash/failure injection at every commit stage defined in `FAULT_INJECTION_TESTS.md`.

### UI Automation

Use `winapp ui`/Windows UI Automation for deterministic tree inspection, search, invocation, screenshots and suitable interactions. Prefer UIA patterns in CI; isolate real input injection to unlocked interactive runners for drag/paste behavior.

### Manual exploratory

Required for Explorer integration feel, mixed-DPI placement, accessibility, cloud/removable edge cases, Store packaging and subjective visual quality. Results are evidence, not a substitute for automated correctness tests.

## 2. OS/architecture matrix

Mandatory V1 release qualification:

- Windows 11 25H2 x64
- Windows 11 25H2 ARM64

Compatibility qualification:

- Windows 11 26H1 on real supported hardware/architecture available to the project, recorded separately.

Do not invent a 26H1 architecture requirement unsupported by actual devices.

## 3. DPI/display matrix

At minimum:

- 100%
- 125%
- 150%
- 175%
- 200%
- mixed-DPI dual-monitor configuration
- 60 Hz and, where available, 120 Hz visual check

## 4. Theme/accessibility matrix

- Light
- Dark
- High Contrast
- text scaling
- reduced motion
- reduced transparency where exposed
- keyboard-only
- Narrator smoke

## 5. Language matrix

- en-US
- tr-TR
- Turkish Windows with English override if manual setting ships
- English Windows with Turkish override if manual setting ships
- long paths and Unicode folder names

## 6. Image/security corpus

Maintain repository-safe generated/original fixtures for:

- tiny PNG;
- transparent PNG;
- large JPEG with EXIF rotation;
- WebP;
- SVG with no external refs;
- malicious SVG attempting file/network external reference;
- GIF/multipage TIFF policy;
- HEIC/AVIF on every claimed architecture;
- corrupt/truncated inputs;
- extreme dimensions/decompression-resource test;
- animation frame-limit test;
- extreme aspect ratio;
- source deleted immediately after apply.

No copyrighted/random web assets are committed as fixtures without clear license/provenance.

## 7. Golden path regression

1. right-click one supported folder;
2. command appears in modern Windows menu;
3. quick panel opens on correct monitor/context;
4. paste/drop/choose image;
5. icon durable state commits;
6. Explorer renders without restart;
7. remove original source;
8. icon remains;
9. rename folder;
10. icon remains;
11. move within supported local scenarios;
12. icon remains;
13. Undo and Reset restore owned prior semantics;
14. unrelated current desktop.ini settings remain;
15. uninstall keep-customizations path preserves applied icon.

## 8. Explorer stress

- repeated context-menu creation/open/close (hundreds/thousands in automated stress where practical);
- rapid invocation;
- app absent/running/processing/updating;
- invalid/virtual/multiple selections;
- package registration/unregistration cycles;
- Explorer crash/hang monitoring and dump attribution.

Any reproducible Ornare-attributable Explorer crash/hang is P0 and release-blocking.

## 9. Coverage policy

Coverage is a diagnostic, not a vanity gate. Nevertheless:

- risk-critical pure logic (desktop.ini semantic merge, state machine, reconciliation, image-policy validation) should target >=90% branch coverage;
- Core/Storage/Imaging owned logic should target >=80% branch coverage where meaningful;
- generated interop/XAML/platform glue is excluded only with explicit rationale.

A high percentage never compensates for missing failure/behavior tests.

## 10. Packaging certification

Run WACK for RC. Test Store-equivalent package identity, clean install, update from prior supported package, uninstall keep-customizations, and package repair/re-registration behavior.
