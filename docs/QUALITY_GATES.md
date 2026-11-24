# Ornare - Quality Gates

A change may only be merged and shipped when every gate below passes. A gate is never
waived by convenience; a gate change requires an accepted ADR. `docs/DEFINITION_OF_READY_DONE.md`
defines what "done" means for a feature; this document defines the automated and manual
checks that prove it.

## G1 - Build

- `dotnet build Ornare.slnx -c Debug` completes with **0 warnings and 0 errors**.
- `dotnet build Ornare.slnx -c Release` completes with **0 warnings and 0 errors** for
  x64 and ARM64.
- The analyzer set configured in `Directory.Build.props` runs in both configurations.

## G2 - Unit and integration tests

- Every test project under `tests/` passes.
- New behaviour ships with a test that fails before the change and passes after it.
- Fault-injection tests (`FaultInjectionCrashRecoveryTests`) pass; a journal change must
  prove idempotency (INV-TX-002).
- Encoding round-trip tests for `desktop.ini` (UTF-16 LE/BE, UTF-8 BOM, ANSI) pass.
- `QuickPanelRuntimeGuardsTests` proves the runtime guards still exist: the staging gate
  (INV-CON-005), the pre-read image bound (INV-CON-006), the preview sequence check
  (INV-CON-007), the type-separated error copy (INV-UI-005), the language subscription
  (INV-UI-006) and a catch-all on every shell COM entry point (INV-SHELL-001). Deleting a
  protection fails the build, not just the manual test.

## G3 - Localization

- Every key exists in all six locales (`en-US`, `tr-TR`, `de-DE`, `fr-FR`, `es-ES`, `pt-BR`).
- Placeholder sets match across locales for every key.
- No English literal is rendered in a non-English UI; `FormatTimeAgo` and similar helpers
  resolve through `AppResources`.
- `LocalizationParityTests` enforces key and placeholder parity, not just dictionary presence.
- The XAML-literal guard matches the exact property: a `Text` assignment cannot excuse an
  unlocalized `PlaceholderText` or `AutomationProperties.Name`. Accessible names therefore fail
  the gate on their own, which is how six English names were found.

## G4 - Accessibility

- Every interactive control has an accessible name and is reachable by keyboard.
- Focus is restored to a sensible control after a state transition.
- High contrast and 200% text scaling do not clip or hide content.
- Narrator can read every status message that carries a result.

## G5 - Performance

- `docs/PERFORMANCE.md` budgets are met on the reference machine.
- Apply latency, Quick Panel open latency and Explorer refresh time are measured, not
  assumed. A regression beyond budget blocks the release.

## G6 - Security

- `docs/THREAT_MODEL.md` is current for any new attack surface.
- Image input is validated (size, frame count, SVG scripting) before decode.
- No secrets, telemetry or network calls are introduced.
- Dependency admission follows `docs/OSS_REUSE.md`.

## G7 - Data integrity

- Migrations are forward-only and idempotent.
- Cross-store mutations use a durable journal plus idempotent reconciliation.
- No flow deletes user data outside Ornare-owned locations.
- Reset/Undo honour INV-DATA-002.

## G8 - Packaging and release

- MSIX packages build for x64 and ARM64 with valid manifests.
- Signing, WACK and install/update/uninstall are verified per `docs/QA_REPORT.md`.
- Release notes describe user-visible changes.
