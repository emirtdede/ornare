---
name: visual-quality-gate
description: Use for Quick Panel or Manager visual design, screenshot review, XAML layout review, theme/DPI/Turkish polish, or preventing generic card-dashboard UI.
---
# Visual Quality Gate Skill

Read `docs/VISUAL_REFERENCE_SPEC.md`, `docs/UI_REVIEW_RUBRIC.md`, `docs/UX_UI_SPEC.md`, `docs/DESIGN_SYSTEM.md`.

- Review hierarchy before effects.
- Remove boundaries that do not encode interaction/selection.
- Compare Light/Dark/High Contrast/Turkish and scaled screenshots.
- Score 10 rubric dimensions; any zero fails, <18/20 fails release UI gate.
- Reject macOS copies, generic SaaS dashboards, card nesting, decorative glass/gradients and effects that do not communicate state.
