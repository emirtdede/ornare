# Contributing to Ornare

All changes must follow `AGENTS.md`, `.agents/rules/`, accepted ADRs, and the domain specifications under `docs/`.

## Required change flow

1. Identify the requirement IDs affected by the change.
2. Read the relevant architecture/security/UI/test specifications.
3. Add or update tests before or with implementation.
4. Keep changes scoped; do not combine unrelated refactors with behavior changes.
5. Run the verification workflow and attach evidence.
6. Update documentation/ADR/traceability when behavior or a frozen decision changes.

## Review minimum

A change is not complete because it compiles. It must satisfy behavior, failure handling, localization, accessibility, performance impact, security, and tests. See `docs/CODE_REVIEW_STANDARD.md` and `docs/DEFINITION_OF_READY_DONE.md`.
