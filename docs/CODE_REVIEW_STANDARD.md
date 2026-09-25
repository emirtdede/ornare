# Code Review Standard

Every non-trivial production change receives an independent review pass by another agent/reviewer context before it is considered complete. The implementation agent must not self-certify risk-critical changes without a separate verification pass.

## Review order

1. **Correctness** — does behavior match requirement and all state transitions?
2. **Data integrity** — can a failure/race/update/reset damage unrelated state?
3. **Security/privacy** — are inputs, paths, codecs, privileges, secrets and diagnostics handled safely?
4. **Platform correctness** — are Windows APIs used according to current documented contracts?
5. **Concurrency/lifetime** — ownership, cancellation, disposal, reentrancy, UI-thread access.
6. **UX/accessibility/localization** — all visible states and non-English/system modes.
7. **Performance** — startup, Explorer callback, image pipeline, allocations/I/O on hot paths.
8. **Maintainability** — clear responsibility, no speculative abstraction or duplicated policy.
9. **Tests** — meaningful behavior/failure coverage, not implementation-mirroring tests.
10. **OSS/supply chain** — no unreviewed copied code/dependency/assets.

## Mandatory reviewer questions

- What invariant would fail if this code executes twice or is interrupted?
- What happens if disk write 1 succeeds and write 2 fails?
- What happens if the target path changes between validation and commit?
- What user state existed before Ornare, and can we restore only what we own without deleting later third-party changes?
- Does this code run inside Explorer? If yes, can it allocate/block/load heavy dependencies?
- Can malformed image/path/clipboard input consume unbounded CPU/memory/disk?
- Are error states distinguishable and actionable?
- Is there a regression test for the bug/risk this change addresses?
- Does the UI still work in Turkish, High Contrast and keyboard-only mode?

## Review rejection triggers

Reject when any of the following appears without an approved ADR:

- broad exception swallowing;
- script-generated product source/UI;
- shell DLL referencing managed/UI/imaging/DB libraries;
- unbounded filesystem enumeration;
- raw desktop.ini overwrite;
- irreversible action without ownership/recovery semantics;
- insecure fallback to make a test pass;
- new package without provenance/license/security review;
- visual change that reintroduces dashboard/card nesting.
