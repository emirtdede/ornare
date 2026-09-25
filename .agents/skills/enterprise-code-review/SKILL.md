---
name: enterprise-code-review
description: Use for independent review of production C#, C++, XAML, filesystem/database mutation, concurrency, migrations, or bug fixes.
---
# Enterprise Code Review Skill

Read `docs/ENGINEERING_STANDARDS.md`, `docs/CODE_REVIEW_STANDARD.md`, `docs/LOGIC_INVARIANTS.md`, and relevant domain specs.

Review in this order: correctness -> data integrity -> security -> platform contract -> concurrency/lifetime -> UX/a11y/i18n -> performance -> maintainability -> tests -> supply chain.

Never accept code because it is elegant if failure ordering or ownership is unclear. Trace operations through injected failure/crash points and verify success UI is coupled to durable state, not optimistic assumptions.
