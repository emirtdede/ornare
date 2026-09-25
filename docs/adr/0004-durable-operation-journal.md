# ADR 0004 — Durable cross-store operation journal

Status: Accepted

## Decision

Use a durable SQLite operation journal plus idempotent reconciliation for folder customization mutations that span app-local DB state and user-folder filesystem state.

## Rationale

SQLite cannot atomically transact with arbitrary target-folder file writes. Treating `desktop.ini`/ICO writes plus DB updates as one rollback-able transaction creates a false correctness guarantee. The journal records intent before user-visible mutation and lets restart/recovery classify and complete/rollback safely.

## Consequences

- mutation engine has explicit commit boundary/state machine;
- startup reconciles unfinished journal rows before new mutations for the same target;
- fault-injection tests cover every stage;
- cleanup remains last and idempotent;
- success UI is tied to durable state, not only shell rendering.
