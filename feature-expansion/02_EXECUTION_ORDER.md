# Execution Order and Dependency Graph

The feature numbers are product IDs, not implementation order. Execute exactly in this order unless a preceding feature document explicitly says it is already proven in the current codebase and only needs a verification gate.

| Order | Feature | Reason |
|---:|---|---|
| 1 | F18 Asset Deduplication | Hardens the storage/cache primitive used by later render features. |
| 2 | F09 SVG Recoloring | Establishes safe semantic SVG manipulation. |
| 3 | F10 Adaptive Contrast | Establishes deterministic palette generation. |
| 4 | F01 Folder Colors | Uses F09+F10 and existing ICO pipeline. |
| 5 | F02 Custom Color Picker | UI on top of F01; no duplicate color logic. |
| 6 | F06 Contain / Cover / Fill | Generalizes placement recipe. |
| 7 | F07 Transparent Padding | Extends placement recipe. |
| 8 | F08 Background Color | Extends render recipe/composition. |
| 9 | F03 Folder Style Templates | Adds versioned template catalog after renderer is stable. |
| 10 | F04 Status Presets | Recipe layer over template/color/badge. |
| 11 | F05 Priority Presets | Same preset engine; no second implementation. |
| 12 | F12 ICO Import | Asset ingestion. |
| 13 | F13 ICO Export | Derived-output reuse. |
| 14 | F14 PNG Export | Derived-output reuse. |
| 15 | F15 Library Tags | Storage model extension. |
| 16 | F16 Custom Collections | Builds on asset metadata. |
| 17 | F17 Library Search | Search indexes after tag/collection schema is final. |
| 18 | F19 Portable Folder Mode | Qualifies stable NTFS identity/path reconciliation before Manager drag/drop and Explorer secondary commands consume it. |
| 19 | F20 Folder Drag & Drop into Manager | UI ingestion after Manager/library model and F19 identity model are stable. |
| 20 | F21 Apply Last Icon | Core command + lightweight Explorer activation. |
| 21 | F22 Undo from Explorer | Secondary Explorer command routing to existing undo engine. |
| 22 | F23 Reset from Explorer | Secondary Explorer command routing to existing reset engine. |
| 23 | F24 CLI + Machine-Readable Output | Adds separate command surface over stable Core operations. |
| 24 | F25 Dry Run | Adds planning mode shared by CLI and future batch work. |
| 25 | F26 Keyboard-First Workflow | Final interaction/accessibility pass over all new surfaces. |
| 26 | F27 Localization Expansion | Translate the stable final string catalog; keep en-US/tr-TR green throughout earlier work. |
| 27 | F11 Per-Size Optical Rendering | **DEFERRED**; document only, do not implement until explicitly reactivated. |

## Gate behavior

- A failed applicable **technical** feature gate blocks the next non-deferred feature.
- The agent does not stop to ask how to fix a normal implementation/test failure: it performs root-cause analysis, fixes the defect and reruns the gate until green.
- Current release scope is x64-first: real x64 runtime evidence is mandatory; ARM64 Release/Native-AOT/package regression remains mandatory where affected, while physical ARM64 runtime is explicitly `NOT PROVEN / PENDING HARDWARE CERTIFICATION` and does **not** block unrelated x64 feature progression.
- Genuine external items such as missing signing credentials, unresolved asset/license ownership or human linguistic review are recorded as external `NOT PROVEN` and do not waive any technical tests that are possible locally.
- F11 is processed at order 27 only to write its exact `DEFERRED BY PRODUCT DECISION` evidence report; no production code, speculative interface or generic resize may be labeled as F11 implementation.
