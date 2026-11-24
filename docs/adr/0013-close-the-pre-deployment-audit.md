# ADR-0013: Close the pre-deployment audit and delete its report

- Status: Accepted
- Date: 2026-11-24

## Context

`docs/PRE_DEPLOY_AUDIT_2026-11-22.md` was the working record of one review: eleven findings,
their line-by-line evidence, the proposed fix for each, and a closure table. By the time every
finding was fixed it had become a second source of truth about the product's state, and it
competed with `docs/QA_REPORT.md` for that role. That is the same defect that `docs/WORK_REPORT.md`
had: two documents describing the same release, each able to drift from the other and from the
code. The audit also grew stale in the opposite direction once new work landed, because it
describes a snapshot of a tree that no longer exists.

The content has three kinds, and each kind already has a permanent home:

1. **Defects and their fixes.** `CHANGELOG.md` under `[Unreleased]` states what changed and why,
   and the code carries the reasoning at the call site. A listener that needs the original
   symptom can read the changelog entry.
2. **Behaviour rules.** `docs/LOGIC_INVARIANTS.md` holds the `INV-*` registry that the audit's
   findings produced (INV-FS-002, 003, 006, 007, INV-TX-001/002, INV-CON-001/002/003,
   INV-UI-004). Code comments cite those identifiers, so the rule survives without the report.
3. **Decisions that changed compatibility or policy.** `docs/adr/0011` (folder-path bound),
   `docs/adr/0012` (release decisions recovered from the deleted build report), and this record.

An audit report is evidence of a past process, not an architecture document. Keeping it in
`docs/` invites a reader to treat a 22 November snapshot as the current state.

## Decision

Delete `docs/PRE_DEPLOY_AUDIT_2026-11-22.md`, and do not replace it with a new audit report.
Verification evidence lives in `docs/QA_REPORT.md` (date-stamped section, current numbers).
Findings that produced an invariant, a test, an ADR or a changelog entry are considered preserved.

The 2026-11-22 audit is closed with this disposition:

| Bucket | Count | Where it lives now |
| :--- | ---: | :--- |
| Bugs and logic flaws | 3 Critical + 6 High + 9 Medium | `CHANGELOG.md`, code comments, regression tests |
| Performance | 5 | `docs/PERFORMANCE.md` (budgets and gates), tests tagged `Performance` |
| Code cleanup | 6 | `CHANGELOG.md` "Changed", plus the architectural guards that enforce it |
| Data integrity and error handling | 6 | `docs/LOGIC_INVARIANTS.md`, `docs/THREAT_MODEL.md` |
| Enhancements | 5 | Closed items in `CHANGELOG.md`; open items in the debt table below |

## Open items that this ADR does not close

These are engineering debts, not findings. They are listed here so deleting the report does not
make them invisible:

| Item | State | Where tracked |
| :--- | :--- | :--- |
| 120 English XAML literals overwritten at runtime, 0 `x:Uid` | Open | `docs/LOCALIZATION.md` |
| `WaitAsync(cancellationToken)` not propagated through every storage call | Open | `docs/ARCHITECTURE.md` section 12 |
| `ErrorCode` to resource-key mapping is still per call site | Open | `docs/ARCHITECTURE.md` section 10 |
| Badge taxonomy (status/priority) borrowed from Folder Marker | Not started | `docs/COMPETITOR_COMPARISON.md` section 5 |
| Icon catalog depth | Not started | `docs/COMPETITOR_COMPARISON.md` section 5 |

## Consequences

- One document fewer that can contradict `QA_REPORT.md`.
- The reasoning survives in the four places the contract already designates (changelog,
  invariants, ADRs, tests) instead of in a report that ages.
- A future reviewer who wants to know "was this found before" searches the changelog and the
  invariant ids, not a dated PDF-like artefact.

## Alternatives considered

- **Keep the audit and mark it historical.** Rejected: a banner is not a guarantee that a reader
  stops at it, and the repair is cheap because the content is already duplicated where it belongs.
- **Move the whole audit into `QA_REPORT.md`.** Rejected: `QA_REPORT.md` answers "what did we run
  and what did we observe", not "what was broken in a previous tree". Mixing the two is how the
  evidence section previously accumulated stale counts.
