# Scripts Policy

Scripts in this folder are **deterministic engineering automation only**. They may check prerequisites/spec integrity, run locked restores/builds/tests/audits, capture performance, or package validated source.

They must never generate product C#/C++/XAML, UI screens, design-system source, SVG product assets, or localized product copy.

## Included helpers

- `check-prereqs.ps1` - non-mutating prerequisite inventory.
- `spec-audit.ps1` - pre-code specification integrity/contradiction guard.
- `verify.ps1` - build/test/dependency verification after bootstrap.
- `dependency-audit.ps1` - dependency/vulnerability visibility.
- `ui-smoke.ps1` - UIA/screenshot helper when `winapp` is available.
- `perf-capture.ps1` - points engineers to the controlled WPR/WPA procedure.
- `quality-gate.ps1` - orchestrates only automatable gate checks; it explicitly cannot self-certify a gate.

A passing script is evidence, not permission to skip manual, hardware, visual, security or release review requirements.
