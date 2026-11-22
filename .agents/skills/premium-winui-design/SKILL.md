---
name: premium-winui-design
description: Use when designing or implementing any Ornare WinUI surface, control, visual state, motion, theme, screenshot review, or UI refactor.
---
# Premium WinUI Design Skill

Read `docs/UX_UI_SPEC.md` and `docs/DESIGN_SYSTEM.md` first.

## Method

1. Define user goal and hierarchy before XAML.
2. Sketch composition as one continuous surface.
3. Remove containers until every remaining boundary has semantic value.
4. Use native theme resources and Segoe UI Variable.
5. Implement deliberate XAML/control templates - never generate UI by script.
6. Use Composition/Toolkit motion only for state continuity.
7. Test Light/Dark/High Contrast, Turkish, keyboard, text/DPI scaling.
8. Capture screenshots and explicitly check for nested cards, generic SaaS aesthetics, excessive glass/borders/accent.

## Apple-inspired reference

Adopt restraint, content-first branding, typography hierarchy, sparse materials and strong drag/drop feedback. Do not use Apple assets.

## Quick-panel priority

The central image action should be understandable in <2 seconds. It is mostly negative space, not a “drop card inside a window card.”
