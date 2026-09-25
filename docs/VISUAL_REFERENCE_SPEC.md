# Visual Reference Specification

This is the concrete visual contract that removes subjective freedom from the implementation agent while preserving responsive behavior. Values are starting design tokens/effective pixels; adjust only when accessibility/localization requires it or visual review demonstrates a measurable improvement.

## 1. Design character

Ornare is a compact premium system utility. It should feel calm before interaction and precise during interaction. Visual quality comes from proportion, typography, whitespace, direct manipulation, crisp iconography and subtle state continuity — not decorative containers.

The reference is the **discipline** of first-party Apple/macOS utilities, not copied Apple components. Windows input, focus, contrast, typography, accessibility, shell and window behavior remain native.

## 2. Quick Panel reference

### Window

- Initial effective size target: **420 × 330**.
- Minimum adaptive width target: **380** before copy/layout changes are considered.
- Content outer inset: **24** left/right; **20–22** top; **18–20** bottom.
- One continuous background surface. Do not draw an inner “main card.”
- Corner/window treatment follows supported Windows/AppWindow behavior; no fake macOS chrome.

### Vertical rhythm

Conceptual layout:

```text
┌────────────────────────────────────────────┐
│  Folder name                         ···   │  top 20–22
│  C:\…\compact path                         │
│                                            │
│                                            │
│                [image glyph]               │
│                                            │
│              Drop an image                 │
│        Click to choose or press Ctrl+V     │
│                                            │
│                                            │
│  Recent                                    │
│  ◻   ◻   ◻   ◻   ◻            Reset       │
└────────────────────────────────────────────┘
```

This diagram describes alignment/hierarchy, not boxes. The central region has **no permanent rectangle in Idle**.

### Header

- Folder name: 17–18 effective px equivalent, semibold, one line.
- Path: 12–13, regular, secondary foreground, one line, middle trimming where supported.
- Header gap: ~4.
- Overflow/settings affordance is low-emphasis and only present when needed.

### Central direct-manipulation region

- Occupies the largest negative-space region: target ~150–170 px vertical.
- Main glyph: 38–44 effective px, crisp vector.
- Primary instruction: 16, medium/semibold.
- Helper: 12–13, secondary.
- Glyph-to-title gap ~14; title-to-helper gap ~6.
- No idle border/card.

**Valid drag**: temporary accent wash/very subtle boundary may appear around the *interaction region*, not as a nested card. Scale max ~1.015. No pulse.

**Invalid drag**: semantic error glyph/copy; no shake.

**Processing**: if operation exceeds ~200 ms, replace helper/state with compact progress feedback in the same central area. Do not insert another panel.

**Success**: glyph transitions to check; primary copy `Icon applied`; `Undo` is visually secondary but directly reachable. Auto-close ~2.5 s unless pointer/focus is within the panel.

### Recent strip

- Hidden entirely when empty.
- Label 12–13 secondary/medium.
- Up to 5 cells, each visual footprint about 40–44 with at least 32 icon preview area and accessible target around 40–44.
- Cells do not have persistent card chrome. Hover/selection gets one subtle background shape.
- Do not show search in Quick Panel.

### Reset

- Appears only when restore is safe/meaningful.
- Secondary text/button treatment, not destructive red by default because it is reversible restoration.

## 3. Manager reference

### Window

- Initial target about **920 × 640** effective px; responsive minimum defined during implementation based on localized content.
- Mica may be the window foundation. Content stays visually flat.
- Navigation width target **176–200**; not a floating rounded sidebar.
- Main content inset **28–32**.

### Navigation

- Library, Recent, Favorites, Folders, Settings.
- Selected item receives a subtle native selection fill, not a bordered card.
- Icon + label; no counters unless they communicate actionable status.

### Library

- Title row: title 24–28 semibold, optional concise secondary action.
- Grid cells use whitespace; no permanent card border.
- Preview target 56–72 depending density.
- Hover: small background selection surface; do not scale enough to cause reflow.
- Search appears only when library size/use warrants it; it is not mandatory empty chrome.

### Folders

- Flat continuous list with separators or row selection states.
- Columns collapse responsively instead of horizontal scrolling for ordinary window sizes.
- Status has icon + text; never color alone.
- Repair action appears only for rows/status requiring it.

### Settings

- Section heading + flat rows + separators.
- No “Appearance card”, “Language card”, “About card.”
- Controls align on a consistent trailing column where possible.

## 4. Token constraints

- Primary spacing scale: 4 / 8 / 12 / 16 / 20 / 24 / 32 / 40.
- Ordinary internal corner radii: restrained 6–10 range; use platform defaults where appropriate.
- Avoid >12 radius except a platform surface/window clearly calls for it.
- Hairline/divider uses theme resources; do not hardcode gray.
- Accent used for active focus/selection/direct manipulation, not decoration.
- Shadows only for transient elevation/window/flyout semantics.

## 5. Motion

- Hover/focus: ~120 ms.
- Standard state transition: 160–180 ms.
- Emphasis/success: max 220 ms.
- No spring/bounce by default.
- Prefer opacity/transform/compositor-friendly properties; do not animate layout sizes continuously.
- Reduced motion removes nonessential transforms while retaining immediate state feedback.

## 6. Design implementation rule

The implementation agent must author XAML and control templates deliberately. It may not generate a screen from HTML/CSS, screenshot tracing, Figma-to-XAML codegen, PowerShell/Python layout generation, or a generic component-library dashboard template.

## 7. Screenshot comparison questions

Before accepting any screen, answer:

1. Can one remove a border/container without reducing comprehension? If yes, remove it.
2. Is the primary action understood in <2 seconds?
3. Is any visual effect present only to make the screen “look premium”? If yes, remove it.
4. Does Light mode look intentionally designed rather than Dark mode inverted?
5. Does Turkish change hierarchy or cause truncation?
6. Does High Contrast remain structurally understandable?
7. Does the screen feel like Ornare, not Windows Settings, a SaaS dashboard, or a macOS copy?
