# Ornare V1 — Public Microsoft Store Candidate (Windows 11 x64)

This directory contains the isolated, certified candidate package intended for initial Microsoft Store submission, per the formal **Product Owner V1 Scope Decision**.

## 1. Candidate Details

- **Package File:** `Ornare_1.0.0.0_x64.msix`
- **Architecture:** `x64` (Intel/AMD 64-bit)
- **Target OS:** Windows 11 Version 24H2 / 25H2 (Build 26100+)
- **SHA-256:** `60DD118E171A7834A8D7DF8FD949C227BE6962E431A5C5BFCE32CFD4E29F55A7`
- **WACK Certification:** `wack_x64_report.xml` (`OVERALL_RESULT="PASS"`, 22 passed, 0 failures)

## 2. Scope & Exclusion Policy

- **ARM64 Exclusion:** Per Product Owner scope decision, ARM64 packages are strictly excluded from the initial public Store submission.
- **ARM64 Artifacts Retention:** Standalone ARM64 package (`Ornare_1.0.0.0_arm64.msix`) and multi-architecture bundle (`Ornare_1.0.0.0_x64_arm64.msixbundle`) are preserved internally in `production_artifacts/release/arm64-validation/` pending physical hardware runtime certification.
- **Zero Unproven Claims:** Public Store listings must specify Windows 11 x64 with no premature roadmap promises or unverified platform claims.
