---
description: Localization, theme, input and accessibility requirements.
---
# Localization & Accessibility

- No hard-coded user-visible strings.
- `en-US` and `tr-TR` resource coverage is mandatory.
- Follow Windows language automatically by default; English fallback.
- Follow Windows theme automatically by default; support Light/Dark/High Contrast.
- Turkish must be natural and human-reviewed.
- Keyboard equivalence exists for essential mouse actions; paste/picker equivalents cover drag-only interaction.
- Every custom control exposes correct UIA semantics.
- Do not use color alone for state.
- Test 100–200% DPI, text scaling and mixed-DPI monitors.
- Respect reduced motion/transparency accessibility settings.
