# Ornare Enterprise Specification Pack Manifest

Prepared for the pre-implementation phase. The pack is intended to make Antigravity implementation deterministic, reviewable and release-gated; it does **not** claim that unimplemented software is defect-free.

The package contains:

- master goal prompt and root agent contract;
- Antigravity rules, skills, role guidance and workflows;
- Microsoft Learn MCP workspace configuration;
- product/V1 scope and requirement traceability;
- logic invariants and explicit state machines;
- premium visual reference specification, design system and UI review rubric;
- architecture, durable operation journal and recovery semantics;
- safe `desktop.ini` ownership/reset semantics and long-path qualification;
- Windows shell-extension, image pipeline, storage and localization/accessibility specifications;
- Microsoft SDL/NIST SSDF-aligned secure-development process;
- threat model, fault-injection plan, risk register and support/diagnostics policy;
- performance budgets and evidence templates;
- OS/architecture compatibility policy;
- test plan, acceptance criteria and fail-closed quality gates;
- CI/CD, supply-chain, lockfile/SBOM, release/signing/versioning policies;
- OSS reuse/provenance and third-party notice framework;
- privacy-statement draft and support playbook;
- ADRs and change-control governance;
- deterministic engineering scripts, including specification audit and gate orchestration;
- production-evidence folder structure;
- source/test scaffolding directories.

Production application code/UI is intentionally absent. Antigravity must author real source directly under `src/` and tests under `tests/` according to these contracts; scripts may not manufacture product code or UI.

## Final enterprise supplements

- `docs/NATIVE_CPP_HARDENING.md`
- `docs/SQLITE_PROVIDER.md`
- `docs/RELEASE_EVIDENCE_MODEL.md`
- `docs/PRE_CODE_FINAL_AUDIT.md`
- `.github/dependabot.yml`
- `.github/ISSUE_TEMPLATE/bug_report.yml`
- `THIRD-PARTY-NOTICES.md` (pre-code baseline; regenerate/reconcile for release)


## Source ownership

See `LICENSE.md` for the proprietary Ornare source notice and `THIRD-PARTY-NOTICES.md` for third-party licensing. Final public distribution terms remain a release/legal gate.
