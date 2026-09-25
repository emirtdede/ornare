---
description: Re-validate a version-sensitive platform/dependency/design assumption from authoritative sources and update project decisions safely.
---
# /research-update

1. State the exact assumption being revalidated.
2. Prefer Microsoft Learn MCP/official upstream docs for Windows; official vendor/upstream source for other dependencies.
3. Record dates/versions and whether evidence applies to supported Windows versions.
4. Compare new evidence with current spec/ADR.
5. If no decision changes, update source notes only if useful.
6. If a decision changes, create/supersede ADR and update every affected spec before code changes.
