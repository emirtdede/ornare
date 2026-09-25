---
description: Freeze a screen/state design before implementing production XAML.
---
# /design-freeze

1. Read UI/UX, Design System, Visual Reference, Screen Specifications and UI Review Rubric.
2. State the user goal, primary action, information hierarchy and every UI state.
3. Produce a textual wireframe and exact token/layout decisions; no script/code-generated UI.
4. Explicitly list containers/boundaries and justify each semantic reason. Remove decorative containers.
5. Review Light/Dark/High Contrast, Turkish expansion, keyboard/focus and reduced motion behavior on paper/spec first.
6. Score the proposed design against `UI_REVIEW_RUBRIC.md`; redesign until expected score is >=18/20 with no zero.
7. Record the frozen decision in `production_artifacts/design/<surface>-design-freeze.md`.
8. Only then implement XAML.
