---
description: Security and privacy review aligned with Ornare threat model and secure SDLC.
---
# /security-review

1. Read `THREAT_MODEL.md`, `SECURE_SDLC.md`, `SECURITY_PRIVACY.md`, `DEPENDENCY_SUPPLY_CHAIN.md`.
2. Identify changed trust boundaries, input formats, privileges, file mutations and dependencies.
3. Review path/reparse/TOCTOU, codec/resource limits, SVG external resources, diagnostics/privacy and shell stability.
4. Run dependency/native vulnerability audit and relevant security/fault corpus.
5. Ensure no fallback weakened Windows security or required hidden elevation.
6. Update threat model/risk register if architecture changed.
7. Write `production_artifacts/security/security-review-<scope>.md` with blocking/non-blocking findings. P0/P1 stays open -> FAIL.
