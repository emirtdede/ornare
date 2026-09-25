---
description: Independent architecture/correctness review for a vertical slice or risk-critical change.
---
# /architecture-review

1. Identify affected ORN-* requirements and INV-* invariants.
2. Read current ADRs, Architecture, State Machines, Folder Customization, Storage and Migration/Recovery docs.
3. Trace happy path and every failure boundary including process termination.
4. Check ownership, idempotence, rollback/reconciliation, reentrancy and cleanup ordering.
5. Check Explorer process boundary and native/managed dependency graph.
6. Check long-path/reparse/cloud/platform assumptions against current official Microsoft documentation.
7. Run or request the relevant tests/fault injection.
8. Write `production_artifacts/architecture/architecture-review-<scope>.md` with PASS/FAIL, evidence and blocking findings.
