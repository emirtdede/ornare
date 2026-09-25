---
description: Dependency pinning, vulnerability, provenance, lockfile and SBOM rules.
---
# Supply Chain

- Follow `docs/DEPENDENCY_SUPPLY_CHAIN.md`.
- Use only approved package sources; no ad-hoc feed to bypass restore problems.
- Pin direct dependencies centrally and commit lock files after projects exist.
- High/Critical vulnerabilities block release; Moderate requires explicit exploitability exception or remediation.
- Every copied OSS source file/snippet has URL, exact commit SHA, path, license and modification record.
- Release produces SBOM, native binary inventory and complete third-party notices.
- Preview/RC dependencies require ADR and a release-blocking necessity.
