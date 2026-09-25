# UI Review Rubric — Release-Blocking Visual Quality

A UI review is evidence-based. “Looks premium to me” is not acceptance.

Score each dimension 0–2. Any **0** in a release-blocking dimension fails the review. Target: 18/20 minimum, with no zero.

| Dimension | 0 — fail | 1 — acceptable | 2 — Ornare bar |
|---|---|---|---|
| Hierarchy | competing CTAs / unclear focal point | understandable | one immediate focal action; secondary controls recede naturally |
| Container discipline | card/panel nesting | some unnecessary boxing | continuous surface; boundaries only for interaction/selection |
| Spacing | inconsistent/crowded | mostly consistent | optical rhythm intentional; whitespace does grouping work |
| Typography | arbitrary sizes/weights | standard | compact, legible, restrained hierarchy; no oversized SaaS headings |
| Materials | glass/shadow decoration | mostly restrained | material explains transient/navigation depth only |
| Color | excessive accent/semantic misuse | generally correct | sparse accent; state communicates without color dependency |
| Motion | bouncy/decorative/janky | functional | short compositor-friendly continuity; reduced-motion equivalent |
| Input feedback | ambiguous drag/click/focus | usable | drag/paste/keyboard states are immediate and unmistakable |
| Accessibility/localization | clipping/focus/contrast failure | passes basics | intentional keyboard/UIA/high-contrast/Turkish quality |
| Platform fit/originality | copied macOS or generic WinUI/SaaS | native enough | original Ornare identity with Windows behavior + Apple-level restraint |

## Mandatory visual anti-pattern rejection

Reject immediately when screenshots show:

- a card containing a card;
- multiple floating panels just to separate headings;
- a bordered drop rectangle inside another bordered surface in idle state;
- gradients/aurora/particles used to manufacture “premium” appearance;
- accent color across most controls;
- oversized logo/hero area;
- more than one primary CTA in the Quick Panel;
- success/failure communicated only by color;
- arbitrary macOS traffic-light/window imitation.

## Review evidence

For every material UI change capture:

- Light 100%;
- Dark 100%;
- one 150–200% DPI screenshot;
- Turkish screenshot;
- High Contrast screenshot for state/interaction changes;
- UIA tree excerpt for essential controls;
- written rubric score and identified follow-up.
