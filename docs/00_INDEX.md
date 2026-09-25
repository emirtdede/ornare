# Ornare Documentation Index

Start with root `AGENTS.md` and `GOAL_PROMPT.md`. This index groups normative project specifications.

## A. Product, governance and traceability

1. `PRODUCT_SPEC.md` — V1 scope, workflows and exclusions.
2. `REQUIREMENTS_TRACEABILITY.md` — immutable ORN-* requirement IDs and verification mapping.
3. `LOGIC_INVARIANTS.md` — correctness invariants implementation may never violate.
4. `STATE_MACHINES.md` — explicit UI, mutation-journal and health transitions.
5. `DEFINITION_OF_READY_DONE.md` — feature/release entry/exit criteria.
6. `QUALITY_GATES.md` — fail-closed milestone gates and P0–P3 defect policy.
7. `GOVERNANCE_CHANGE_CONTROL.md` — ADR/change/review authority and evidence rules.
8. `PROJECT_DECISIONS.md` — frozen project decisions.
9. `IMPLEMENTATION_ROADMAP.md` — ordered gates, not timeline estimates.

## B. UI/UX and accessibility

10. `UX_UI_SPEC.md` — interaction philosophy/behavior.
11. `VISUAL_REFERENCE_SPEC.md` — concrete visual/layout contract.
12. `UI_REVIEW_RUBRIC.md` — objective visual quality gate.
13. `DESIGN_SYSTEM.md` — tokens/components/material/motion/iconography.
14. `SCREEN_SPECIFICATIONS.md` — every V1 surface/state.
15. `INFORMATION_ARCHITECTURE.md` — navigation/content organization.
16. `COPY_CATALOG_SEED.md` — initial en-US/tr-TR product copy.
17. `LOCALIZATION.md` — resources/language behavior.
18. `ACCESSIBILITY.md` — keyboard/UIA/contrast/scaling/reduced motion.

## C. Architecture and platform

19. `ARCHITECTURE.md` — process/project boundaries, activation, durable mutation/recovery and concurrency.
20. `PLATFORM_COMPATIBILITY.md` — 25H2/26H1, architectures and filesystem support.
21. `LONG_PATH_COMPATIBILITY.md` — MAX_PATH/API constraint and fail-safe qualification.
22. `SHELL_EXTENSION.md` — Explorer integration contract.
23. `FOLDER_CUSTOMIZATION.md` — desktop.ini ownership/preservation/reset/repair.
24. `IMAGE_PIPELINE.md` — secure decode/normalization/ICO pipeline.
25. `STORAGE_DATA_MODEL.md` — SQLite, assets, baselines, journal, cleanup.
26. `MIGRATION_RECOVERY.md` — schema/upgrade/uninstall/recovery policy.

## D. Engineering, security and supply chain

27. `ENGINEERING_STANDARDS.md` — enterprise C#/C++/XAML/filesystem standards.
28. `CODE_REVIEW_STANDARD.md` — independent review criteria.
29. `SECURITY_PRIVACY.md` — product security/privacy requirements.
30. `THREAT_MODEL.md` — trust boundaries/abuse cases/controls.
31. `SECURE_SDLC.md` — Microsoft SDL/NIST SSDF-aligned development process.
32. `DEPENDENCY_SUPPLY_CHAIN.md` — pins/locks/audit/SBOM/provenance.
33. `OSS_REUSE.md` — approved source/package reuse policy.
34. `LOGGING_DIAGNOSTICS.md` — no-telemetry local diagnostics policy.
35. `RISK_REGISTER.md` — current engineering/product risks and proof gates.
36. `AI_AGENT_SECURITY.md` — prompt-injection/tool/MCP/secret safety.
37. `VERSIONING_RELEASE_POLICY.md` — semantic/product/schema/release-line versioning.

## E. Verification, performance, operations and release

38. `TEST_PLAN.md` — automated/manual/risk test matrix.
39. `FAULT_INJECTION_TESTS.md` — crash consistency and mutation failure injection.
40. `PERFORMANCE.md` — P50/P95 benchmark contract and targets.
41. `ACCEPTANCE_CRITERIA.md` — V1 release-blocking checks.
42. `CI_CD_SPEC.md` — locked build/test/security/release pipeline.
43. `RELEASE_DISTRIBUTION.md` — MSIX/Store/signing/update/release artifacts.
44. `SUPPORT_PLAYBOOK.md` — privacy-preserving support and incident triage.
45. `PRIVACY_STATEMENT_DRAFT.md` — release/legal-review privacy draft.
46. `VERSION_BASELINE.md` — verified 2026-09-24 dependency/toolchain baseline.
47. `BRAND_NAME_CLEARANCE.md` — pre-publication Ornare naming gate.
48. `ENTERPRISE_READINESS_AUDIT.md` — pre-code audit and remaining implementation proofs.

## F. Agent/tooling/research

49. `ANTIGRAVITY_PLAYBOOK.md` — execution sequence for Antigravity.
50. `TOOLING_MCP_CLI.md` — Antigravity, Microsoft Learn MCP, winapp CLI and build tools.
51. `RESEARCH_SOURCES.md` — primary authoritative external sources.

## Decision records

`docs/adr/` records architecture decisions. Accepted ADRs may supersede earlier explanatory text only when all affected specifications are updated in the same change.

## Evidence and templates

- `production_artifacts/` — milestone evidence only, never generated production source/UI.
- `templates/` — architecture/security/UI/performance/quality/release report structures.
- `scripts/spec-audit.ps1` — pre-code specification integrity guard.
- `scripts/quality-gate.ps1` — runs automatable checks but cannot self-certify a quality gate.

## Rule hierarchy

Root `AGENTS.md` and numbered `.agents/rules/` are the execution contract. `REQUIREMENTS_TRACEABILITY.md`, `LOGIC_INVARIANTS.md`, `STATE_MACHINES.md`, and `QUALITY_GATES.md` are normative. If implementation evidence invalidates an assumption, stop guessing, collect evidence, create/update an ADR and reconcile all impacted documents in the same change.

## Enterprise hardening supplements

- `NATIVE_CPP_HARDENING.md` — Explorer DLL compiler/linker/runtime hardening and evidence.
- `SQLITE_PROVIDER.md` — Windows system SQLite provider decision and constraints.
- `RELEASE_EVIDENCE_MODEL.md` — what constitutes gate/release proof.
- `PRE_CODE_FINAL_AUDIT.md` — final specification readiness audit.
