# Secure Software Development Lifecycle

Ornare adopts a lightweight but formal secure-development process aligned with Microsoft's current SDL practice model and NIST SP 800-218 SSDF **v1.1 final**. NIST SSDF v1.2 is still treated as draft/informational until a final revision supersedes v1.1. This is an engineering process standard, not a compliance/certification claim.


## 0. Microsoft SDL practice mapping

Ornare explicitly covers the current Microsoft SDL themes: security governance/metrics; proven security features/frameworks; threat modeling/design review; cryptography policy where cryptography is introduced; software-supply-chain security; engineering-environment security; security testing; operational platform security; monitoring/response; and role-appropriate security knowledge. Features that do not use cryptography must not invent custom cryptography merely to satisfy a checklist.

## 1. Prepare / governance

- security/privacy/data-integrity requirements are written before implementation;
- supported OS/package/dependency policies are documented;
- risk register and threat model are versioned;
- agent/tool access follows least privilege;
- release/signing credentials never enter source or agent prompts.

## 2. Protect the software

- source repository access and release permissions are separated where the hosting platform permits;
- dependencies are pinned/locked and inventoried;
- release artifacts are signed and hashed;
- provenance/third-party notices are retained;
- no binary/source copied from an unknown origin.

## 3. Produce well-secured software

Design:

- threat model updated for architecture/input/storage changes;
- least privilege and standard-user operation;
- explicit trust boundaries for Explorer, input decoders and filesystem;
- fail-safe behavior for unsupported/remote/protected targets.

Implementation:

- analyzers/warnings/security compiler mitigations enabled;
- input/resource/path validation;
- no unsafe security fallback;
- secrets scanning where platform CI supports it;
- no network access for core image operation.

Verification:

- unit/integration/fault/security corpus;
- dependency vulnerability audit;
- shell stress/crash investigation;
- WACK;
- independent review pass for risk-critical changes.

## 4. Respond to vulnerabilities

Before public release establish:

- private reporting path (repository private security advisory/private reporting);
- severity triage using P0–P3 policy;
- ability to revoke/replace a compromised package/signing path;
- dependency advisory response procedure;
- release notes/security notice process;
- root-cause follow-up test for security defects.

## 5. Release security evidence

RC retains:

- threat-model delta review;
- dependency audit results;
- SBOM;
- native binary inventory;
- package/signature verification;
- high/critical finding disposition;
- fault-injection results for mutation engine;
- shell stress results;
- privacy/diagnostic review.

## 6. Reference baseline

Primary references are listed in `RESEARCH_SOURCES.md`, including Microsoft SDL and NIST SSDF. If those frameworks change, update this document only where the project practice materially changes; do not copy compliance language blindly.
