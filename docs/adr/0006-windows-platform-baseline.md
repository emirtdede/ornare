# ADR 0006 — Windows 11 25H2 baseline with applicable 26H1 qualification

Status: Accepted

## Decision

V1 officially targets Windows 11 25H2 (build family 26200) on x64 and ARM64. Windows 11 26H1 is compatibility-tested on actual supported commercially representative hardware/architecture rather than declaring a fictitious mandatory architecture matrix.

## Rationale

Windows 11 24H2 Home/Pro reaches end of servicing near the planned release window. Microsoft positions 26H1 as a special platform release for specific silicon rather than a normal in-place feature update from 25H2.

## Consequences

- package/runtime minimum is 25H2 policy;
- compilation may use newer stable Windows SDK;
- platform-specific APIs require compatibility checks;
- 26H1 results are tracked separately.
