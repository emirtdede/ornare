# V1 Acceptance Criteria

Every P0 criterion is release-blocking. Requirement IDs map through `REQUIREMENTS_TRACEABILITY.md`.

## P0 — Core behavior

- [ ] ORN-FUN-001: exactly one supported folder receives modern Windows 11 `Set Folder Icon` action.
- [ ] Multiple-folder selection performs no ambiguous partial mutation.
- [ ] Quick Panel opens without loading the full Manager.
- [ ] ORN-FUN-002/003/004: Drop, picker and Ctrl+V supported inputs work.
- [ ] User never has to prepare/select an ICO for ordinary use.
- [ ] Valid input applies without an Apply confirmation button.
- [ ] Explorer displays the new icon without normal-flow Explorer restart/cache deletion.
- [ ] ORN-FUN-005: source image can be deleted/moved after apply without breaking the icon.
- [ ] ORN-FUN-006: folder rename and qualified local move preserve the icon.
- [ ] ORN-FUN-007: Reset restores only pre-Ornare owned icon semantics while preserving unrelated current settings.
- [ ] ORN-FUN-008: Undo restores the previous successful Ornare state.
- [ ] ORN-REL-004: uninstall keep-customizations leaves applied icons functional.

## P0 — Data integrity / crash consistency

- [ ] Existing unrelated `desktop.ini` values survive apply/reset/repair, including values added after the first Ornare operation.
- [ ] Operation journal is created before first user-visible cross-store mutation and unfinished operations reconcile deterministically after restart.
- [ ] Fault injection at each defined commit point cannot cause unrelated file loss or falsely report a half-applied state as healthy.
- [ ] Ornare never deletes unrelated files based on filename pattern alone.
- [ ] Protected/unsupported target refusal does not request hidden elevation.
- [ ] External icon-semantic conflict is detected instead of silently overwritten.
- [ ] ORN-REL-005: long-path targets either pass the qualified safe implementation or are rejected before mutation; no path truncation/wrong-target write.
- [ ] ORN-REL-006: simultaneous/duplicate activation for the same folder cannot create concurrent writers or split ownership.
- [ ] ORN-REL-007: unsupported target classes are rejected before mutation; directory ReadOnly shell attribute is not mistaken for ACL write denial; target reparse-point directories are not followed in V1.
- [ ] ORN-REL-008: an icon changed externally after Ornare apply is never overwritten by automatic Reset/Repair; explicit reapply re-baselines the live external state first.

## P0 — Explorer safety

- [ ] Shell DLL has no app UI, imaging, DB, network, managed-runtime-heavy dependency path.
- [ ] Explorer stress suite produces zero Ornare-attributable crash/hang.
- [ ] Shell callback latency is measured and meets/has explicitly resolved release budget.

## P0 — UI quality

- [ ] Quick Panel passes `UI_REVIEW_RUBRIC.md` >=18/20 with no zero.
- [ ] No screen uses nested card/panel dashboard composition.
- [ ] Quick Panel has one visually dominant action.
- [ ] Light and Dark modes are intentionally designed.
- [ ] High Contrast remains structurally usable.
- [ ] 100–200% DPI and mixed-DPI placement work.
- [ ] No clipped/illegible Turkish UI in supported scaling matrix.
- [ ] No Apple proprietary asset appears in source/package.
- [ ] No emoji is used as product iconography.

## P0 — Localization/accessibility

- [ ] en-US complete.
- [ ] tr-TR complete and human-reviewed.
- [ ] System language/theme followed by default.
- [ ] Keyboard-only picker/paste golden path is usable.
- [ ] UIA tree exposes meaningful names/roles/states.
- [ ] Narrator smoke + text scaling + reduced-motion checks pass.

## P0 — Security/privacy

- [ ] No custom telemetry/analytics/network image upload/account dependency.
- [ ] Image resource/coder policy active and tested.
- [ ] SVG external/network access blocked.
- [ ] Reparse/path-race tests prove unsafe redirection does not silently write elsewhere.
- [ ] NuGet/native dependency security review passes release policy.
- [ ] No secret embedded in repo/package/logs.

## P0 — Distribution/supply chain

- [ ] Windows 11 25H2 x64 packaged Release passes runtime matrix.
- [ ] Windows 11 25H2 ARM64 packaged Release passes on real ARM64 Windows hardware.
- [ ] 26H1 compatibility tested where applicable supported hardware exists.
- [ ] MSIX bundle install/update/uninstall passes.
- [ ] WACK passes or only explicitly reviewed non-blocking informational items remain.
- [ ] SBOM, native binary inventory and OSS notices complete.
- [ ] final brand/name clearance complete before public Store listing.
- [ ] zero open P0/P1 defects.


## Requirement-ID completeness checklist

This mechanically prevents a traced V1 requirement from disappearing inside prose. Every item below must have implementation + verification evidence before Gate 4; Gate 5/6 additionally apply release-specific criteria.

- [ ] `ORN-FUN-001` evidence linked in the quality-gate/release report.
- [ ] `ORN-FUN-002` evidence linked in the quality-gate/release report.
- [ ] `ORN-FUN-003` evidence linked in the quality-gate/release report.
- [ ] `ORN-FUN-004` evidence linked in the quality-gate/release report.
- [ ] `ORN-FUN-005` evidence linked in the quality-gate/release report.
- [ ] `ORN-FUN-006` evidence linked in the quality-gate/release report.
- [ ] `ORN-FUN-007` evidence linked in the quality-gate/release report.
- [ ] `ORN-FUN-008` evidence linked in the quality-gate/release report.
- [ ] `ORN-FUN-009` evidence linked in the quality-gate/release report.
- [ ] `ORN-FUN-010` evidence linked in the quality-gate/release report.
- [ ] `ORN-REL-001` evidence linked in the quality-gate/release report.
- [ ] `ORN-REL-002` evidence linked in the quality-gate/release report.
- [ ] `ORN-REL-003` evidence linked in the quality-gate/release report.
- [ ] `ORN-REL-004` evidence linked in the quality-gate/release report.
- [ ] `ORN-REL-005` evidence linked in the quality-gate/release report.
- [ ] `ORN-REL-006` evidence linked in the quality-gate/release report.
- [ ] `ORN-REL-007` evidence linked in the quality-gate/release report.
- [ ] `ORN-REL-008` evidence linked in the quality-gate/release report.
- [ ] `ORN-SEC-001` evidence linked in the quality-gate/release report.
- [ ] `ORN-SEC-002` evidence linked in the quality-gate/release report.
- [ ] `ORN-SEC-003` evidence linked in the quality-gate/release report.
- [ ] `ORN-SEC-004` evidence linked in the quality-gate/release report.
- [ ] `ORN-SEC-005` evidence linked in the quality-gate/release report.
- [ ] `ORN-PRV-001` evidence linked in the quality-gate/release report.
- [ ] `ORN-UX-001` evidence linked in the quality-gate/release report.
- [ ] `ORN-UX-002` evidence linked in the quality-gate/release report.
- [ ] `ORN-UX-003` evidence linked in the quality-gate/release report.
- [ ] `ORN-UX-004` evidence linked in the quality-gate/release report.
- [ ] `ORN-PERF-001` evidence linked in the quality-gate/release report.
- [ ] `ORN-PERF-002` evidence linked in the quality-gate/release report.
- [ ] `ORN-PKG-001` evidence linked in the quality-gate/release report.
- [ ] `ORN-SC-001` evidence linked in the quality-gate/release report.
