---
description: Requirements, invariants, readiness and traceability rules that apply before implementation.
---
# Requirements & Traceability

- Read `docs/REQUIREMENTS_TRACEABILITY.md`, `docs/LOGIC_INVARIANTS.md`, and `docs/DEFINITION_OF_READY_DONE.md` before implementing behavior.
- Every feature/change identifies affected ORN-* requirement IDs and INV-* invariants.
- Do not invent behavior not present in V1 scope to make implementation easier or more impressive.
- Unspecified version-sensitive Windows behavior must be researched from current official sources, not guessed.
- A genuinely unresolved architectural question becomes a measured spike/ADR before production implementation.
- Never mark a requirement complete with implementation evidence only; include verification evidence.
