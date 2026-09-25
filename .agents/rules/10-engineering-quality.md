---
description: Production code quality, architecture, and source-authoring rules.
---
# Engineering Quality

`docs/ENGINEERING_STANDARDS.md`, `docs/STATE_MACHINES.md`, and `docs/LOGIC_INVARIANTS.md` are normative.

- No placeholders, fake data, empty methods, dead controls, or “temporary” production bypasses.
- No giant god classes or giant XAML pages. Factor by domain responsibility and reusable semantic behavior.
- Avoid overengineering: no repository/service abstractions when they add no testability or isolation value.
- Prefer explicit typed results/errors over booleans and swallowed exceptions.
- Use async/cancellation correctly; never block UI/Explorer threads on heavy work.
- Use `x:Bind`/compiled binding where appropriate; keep visual trees lean.
- Lazy-init heavy services.
- Treat warnings as defects where practical; enable analyzers/nullable and maintain clean Release builds.
- Comments explain non-obvious constraints/why, not line-by-line mechanics.
- Every file is deliberately authored; no bulk agent-generated boilerplate through helper scripts.
