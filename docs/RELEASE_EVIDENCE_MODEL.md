# Release Evidence Model

A quality gate is a decision backed by artifacts, not an agent statement.

## Required evidence classes

| Area | Minimum evidence |
|---|---|
| Build | commit/tag, toolchain versions, locked restore result, x64/ARM64 build logs |
| Correctness | unit/integration/UIA summaries mapped to requirement IDs |
| Data integrity | fault-injection journal/reconciliation report, reset/conflict regression evidence |
| Explorer safety | shell latency measurement, stress run, crash/hang attribution summary |
| Security | threat-model delta, dependency audit, native static analysis, image corpus results |
| Performance | named machine profile, Release build hash, cold/warm P50/P95, memory/apply metrics |
| UI | Light/Dark/High Contrast/Turkish/DPI screenshots + rubric score + accessibility notes |
| Packaging | WACK, clean install/update/uninstall evidence, package identity/version/hash |
| Supply chain | SBOM, native binary hashes, THIRD-PARTY-NOTICES, copied-source provenance |
| Release | approved gate report, immutable source ref, package signature/hash, rollback instructions |

## Evidence rules

- Evidence is immutable per candidate; rerunning a test creates a new record rather than editing history silently.
- Every artifact records app version, commit, architecture, Windows build, and timestamp where applicable.
- A screenshot alone does not prove behavior; a log alone does not prove visual quality.
- A passing automated script is evidence input, not gate certification.
- Missing hardware evidence is `NOT PROVEN`, never `PASS`.

## Suggested repository-local staging

`production_artifacts/` may contain local milestone evidence while developing, but sensitive dumps/paths/secrets must not be committed. Public release evidence should contain sanitized reports and hashes rather than private crash dumps or user data.
