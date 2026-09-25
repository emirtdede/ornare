# Governance and Change Control

Ornare uses lightweight enterprise governance: enough control to protect Explorer stability, user files, security and UX without creating process theatre.

## 1. Decision authority

The written specification is authoritative. A code change may not silently change product behavior, supported platform policy, storage semantics, security posture or V1 scope.

Changes in these categories require an ADR or an explicit update to the existing ADR and all affected requirements/tests:

- Explorer/shell-extension architecture;
- persistence, `desktop.ini`, icon ownership, reset or migration semantics;
- supported Windows versions/architectures;
- image decoder/security policy;
- package identity/distribution/signing;
- privacy/network/telemetry behavior;
- public API/protocol/data-schema compatibility;
- foundational UI/design-system direction.

## 2. Risk-based approval perspectives

No named employee is assumed. Reviews are role-based and evidence-driven.

- **Product/UX:** user-facing behavior, copy, scope, interaction hierarchy.
- **Native Windows:** shell, package, OS/API compatibility, filesystem semantics.
- **Security/Reliability:** untrusted input, mutation/recovery, dependency/security impact.
- **Performance:** startup/shell/apply regressions and background-resource changes.
- **QA/Release:** testability, matrix, packaging, migration, release evidence.

A single person/model may temporarily play multiple roles, but a risk-critical implementation pass must not self-certify; use a fresh review context/pass as required by `CODE_REVIEW_STANDARD.md`.

## 3. Change record

Every substantive feature/change must leave:

- requirement IDs affected;
- rationale;
- files changed;
- test evidence;
- security/reliability/performance impact statement;
- migration/compatibility statement;
- unresolved risk/issue links if any.

Use `templates/FEATURE_IMPLEMENTATION_REPORT.md`.

## 4. No silent scope expansion

A useful idea is not permission to implement it. Anything outside V1 must be recorded for a later milestone rather than added opportunistically.

## 5. Emergency fixes

A P0/P1 fix may use an accelerated review path, but must still include regression evidence and a post-fix review. Emergency status never permits bypassing signing, dependency integrity, or destructive-data safeguards.
