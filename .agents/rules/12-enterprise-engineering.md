---
description: Enterprise source quality, correctness, concurrency and review standards.
---
# Enterprise Engineering

- `docs/ENGINEERING_STANDARDS.md` is normative.
- Nullable/analyzers/warnings-as-errors remain enabled; fix warnings instead of suppressing broadly.
- No `async void` except framework event handlers; no UI-thread blocking on I/O/CPU work.
- No hidden global mutable state, fire-and-forget work, or ambiguous boolean-only failure results.
- Filesystem + SQLite mutations use durable journaling/reconciliation; do not pretend a DB transaction rolls back arbitrary filesystem writes.
- Re-read/revalidate path identity and current desktop.ini state immediately before mutation.
- Reset/Undo never overwrite a newer full desktop.ini snapshot; merge only owned icon semantics.
- Any bug fix adds a regression test where feasible.
- Risk-critical changes require an independent review perspective using `docs/CODE_REVIEW_STANDARD.md`.
