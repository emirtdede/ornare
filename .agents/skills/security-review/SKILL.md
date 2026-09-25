---
name: security-review
description: Use for threat modeling, filesystem writes, unsafe images/SVG, native dependencies, secrets, diagnostics, package security, or pre-release security review.
---
# Security Review Skill

For a changed feature identify trust boundaries, attacker-controlled inputs, privileges, file paths/reparse points, native parser surface, rollback behavior, secrets and privacy impact.

Block release for:

- unrestricted image delegates/network fetch;
- unsafe recursive deletion;
- user file overwrite risk;
- Explorer crash/hang risk;
- secrets in repo/package;
- high/critical known vulnerable shipped dependency without accepted mitigation.
