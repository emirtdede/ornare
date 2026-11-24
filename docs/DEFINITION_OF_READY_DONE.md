# Ornare - Definition of Ready and Done

A feature is **ready** to start only when the items under "Ready" are satisfied. A feature is
**done** only when every item under "Done" is satisfied. Compilation alone is not completion.

## Ready

- The user-visible behaviour is described, including failure behaviour.
- The affected invariants (`docs/LOGIC_INVARIANTS.md`) are identified.
- New UI has a design intent consistent with `docs/DESIGN_SYSTEM.md` (no card-dashboard
  language, no nested card composition).
- New strings are planned for all six locales.
- New dependencies or assets have passed `docs/OSS_REUSE.md`.
- The security impact is stated and, if new surface exists, `docs/THREAT_MODEL.md` is updated.
- The performance impact is estimated against `docs/PERFORMANCE.md` budgets.

## Done

### Behaviour
- The happy path and every documented failure path are implemented.
- Failures return an `OperationResult` with a localized `ErrorMessage` and a technical
  `DiagnosticDetail`; raw exception text never reaches a user surface.
- Cancellation is honoured and never surfaces as an error.

### UI and localization
- The surface follows the design system and the visual quality gate.
- Every user-visible string resolves through `AppResources` in all six locales.
- Turkish and English are both verified, including text expansion.
- Dark, light and high-contrast themes are verified at the supported scale range.

### Accessibility
- Keyboard navigation, focus order and focus restoration are verified.
- Every control and status message has an accessible name and help text where needed.
- Narrator reads the result of an operation.

### Tests
- Unit tests cover the new logic, including boundary and failure cases.
- Integration tests cover the cross-store or filesystem behaviour where applicable.
- A regression test exists for every bug fix.
- `docs/QUALITY_GATES.md` passes.

### Data and security
- The change cannot lose or corrupt user data.
- Journal/reconciliation behaviour matches the invariants.
- No secret, telemetry, network call or hidden administrator requirement is introduced.

### Performance
- The change meets the relevant budgets or documents why it does not apply.

### Documentation and traceability
- The requirement is recorded in `docs/REQUIREMENTS_TRACEABILITY.md`.
- Architecture, storage, packaging or public-behaviour changes have an ADR.
- User-facing changes are in `CHANGELOG.md`.
