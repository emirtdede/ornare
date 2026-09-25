# Risk Register

| ID | Risk | Likelihood | Impact | Mitigation / proof required | Gate |
|---|---|---:|---:|---|---|
| R-001 | Explorer crash/hang from shell DLL | M | Critical | tiny native DLL; no heavy deps; stress test; dump analysis | G1/G5 |
| R-002 | desktop.ini clobbers unrelated user/third-party state | M | Critical | owned-field merge; conflict detection; fault tests | G1/G2 |
| R-003 | DB/filesystem split-brain after crash | M | High | durable operation journal + idempotent reconciliation | G2 |
| R-004 | image codec vulnerability/resource bomb | M | Critical | current Magick.NET; allowlist/policy; dimensions/time/memory limits; fuzz/corrupt corpus | G2/G5 |
| R-005 | reparse/path TOCTOU writes outside target | L-M | Critical | canonicalize + identity revalidation + reparse policy just before commit | G2 |
| R-006 | Native AOT breaks dependency/runtime behavior | M | Medium | measured vertical-slice experiment; non-AOT ADR fallback | G1 |
| R-007 | ARM64 compiles but fails at runtime/shell | M | High | real ARM64 release qualification | G5 |
| R-008 | icon refresh unreliable/cached | M | High | targeted notification + verification + repair state; no Explorer kill | G1/G4 |
| R-009 | OneDrive/cloud semantics differ | H | Medium | explicit caveat matrix; local behavior tests; no cross-device guarantee | G4 |
| R-010 | premium UI becomes generic card dashboard | M | High | visual spec/rubric/screenshots/design gate | G3 |
| R-011 | Turkish/text scaling clips UI | M | Medium | resource-only copy; expansion tests; responsive sizing | G3/G4 |
| R-012 | supply-chain/native dependency compromise | L-M | Critical | pins/locks/audit/SBOM/hash/signing/provenance | G5 |
| R-013 | Store/MSIX shell registration differs from dev | M | High | packaged tests early; WACK; clean VM tests | G1/G5 |
| R-014 | Ornare name unavailable for software category | M | High | legal/Store/domain clearance before public listing; rename isolated from architecture | G5 |
| R-015 | performance target unrealistic on low-end hardware | M | Medium | named benchmark tiers; P50/P95; optimize before hardening public targets | G4/G5 |
| R-016 | long path exceeds SHGetSetFolderCustomSettings documented limit | M | Medium | explicit long-path test; direct desktop.ini fallback only if proven safe; otherwise unsupported with clear message | G1/G4 |
