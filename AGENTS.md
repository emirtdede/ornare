# Ornare Agent Contract

This file is the non-negotiable contract for every human or AI agent working on Ornare.

## Priority order

1. Safety, security, user data integrity.
2. The user's explicit project requirements.
3. This `AGENTS.md`.
4. `docs/LOGIC_INVARIANTS.md`, `docs/QUALITY_GATES.md`, and `docs/REQUIREMENTS_TRACEABILITY.md`.
5. `.agents/rules/` in numeric order.
6. Accepted ADRs. An ADR may refine architecture but may not silently waive an invariant, security/data-integrity rule, or release gate.
7. Domain specifications in `docs/`.
8. Implementation convenience.

## Definition of done

A feature is done only when `docs/DEFINITION_OF_READY_DONE.md` is satisfied: behavior, UI, localization, accessibility, tests, failure handling, security, performance implications, traceability and documentation are complete. Compilation alone is not completion.

## Non-negotiable product rules

- Windows 11 native WinUI 3 product.
- Premium minimalist UI; no card-dashboard visual language.
- No nested panel/card composition except where a native semantic control truly requires containment.
- No Apple proprietary assets.
- No script-generated product source/UI.
- No account, cloud dependency, telemetry, ads, or image upload in V1.
- No hidden administrator requirement.
- Preserve prior folder customization and user data.
- Never kill/restart Explorer as a normal application flow.
- Shell extension must remain tiny, native, and fail-safe.
- English and Turkish are first-class; system theme/language by default.
- x64 and ARM64 are first-class on the Windows 11 25H2 baseline.
- Windows 11 26H1 is qualified on actual supported hardware/architecture; do not invent unavailable test targets.
- Cross-filesystem/SQLite mutations require durable journal + idempotent reconciliation.
- Reset/Undo may never overwrite a newer full `desktop.ini` snapshot; merge only owned icon semantics.

## How to resolve uncertainty

Use current official documentation, preferably via Microsoft Learn MCP for Windows-specific questions. Write an ADR when a decision changes architecture, compatibility, security, storage, packaging, public behavior, migration semantics, or a frozen quality/performance policy. Never resolve uncertainty by weakening a gate.

## Prohibited shortcuts

- generating XAML/C#/C++ with scripts;
- replacing deliberate component design with copied templates;
- copying unlicensed code/assets;
- inserting placeholders and calling the feature complete;
- skipping tests because behavior looks correct once;
- hiding failures behind `try/catch` and generic success UI;
- changing scope or framework to make implementation easier.
