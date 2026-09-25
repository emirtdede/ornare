# Pre-Code Final Audit — 2026-09-24

Status: **READY FOR GATE 0 / IMPLEMENTATION BOOTSTRAP**.

This audit means the specification set is internally ready to begin implementation. It does **not** claim an unimplemented product has zero defects. Ornare's policy is that unproven implementation behavior remains `NOT PROVEN` until evidence exists.

## Closed specification risks

- Windows-only WinUI 3 architecture and native Explorer boundary are frozen.
- Modern Windows 11 context-menu mechanism and package-identity dependency are explicit.
- 25H2 x64 + ARM64 launch baseline and 26H1 applicable-hardware qualification are explicit.
- `desktop.ini` ownership, reset, external-conflict, rename/move, uninstall and repair semantics are defined.
- Full-file historical restore is prohibited; only owned icon semantics are merged.
- Cross-filesystem + SQLite false-transaction assumptions are eliminated by durable journal/reconciliation.
- Reparse/TOCTOU/unsupported-target behavior is fail-closed.
- Image decoder/resource/SVG/network boundaries are defined.
- SQLite uses Windows system `winsqlite3.dll` through the supported bundle model and avoids optional extension dependencies.
- Native shell compiler/linker hardening and static/dynamic analysis expectations are explicit.
- UI hierarchy, dimensions, motion, theme, localization and anti-card rules are release-gated.
- Script-generated production source/UI is prohibited while deterministic engineering automation remains allowed.
- Requirements traceability, defect severity, review independence, SBOM/provenance and release evidence are defined.

## Intentionally unresolved until code/hardware exists

These are **gates**, not missing product decisions:

1. Native AOT compatibility with final WinUI/Magick.NET/SQLite graph.
2. Actual cold/warm startup and apply P50/P95.
3. Explorer rendering/cache behavior on the final implementation.
4. Real ARM64 runtime behavior.
5. Long-path folder-customization qualification.
6. WACK/Store certification result.
7. Implemented visual/accessibility quality.
8. Final trademark/Store-name clearance.

No agent may turn one of these into an assumed pass.

## Start authorization

Proceed only in this order:

1. `/bootstrap`
2. Gate 0 evidence
3. `/vertical-slice`
4. Gate 1 architecture review
5. feature implementation

Do not build the complete Manager/library before the vertical slice proves the Explorer → activation → image → durable icon → reset/undo chain.
