# Accessibility Specification

Accessibility is a release requirement.

## 1. Keyboard

Every interactive operation available by mouse must have a keyboard path unless the operation is inherently drag-specific, in which case an equivalent file picker/paste path exists.

Quick panel:

- focus enters at a predictable action;
- Tab order follows visual hierarchy;
- Enter/Space activates focused controls;
- Ctrl+V works where expected;
- Escape closes;
- Undo is keyboard accessible.

## 2. UI Automation

All custom controls expose correct UIA roles/patterns/names. Decorative visuals are not announced. Status changes such as success/error use accessible live/status patterns where appropriate without causing excessive announcements.

## 3. Contrast and color

- meet applicable Windows/WCAG contrast expectations;
- High Contrast mode receives a deliberate design, not a visual accident;
- color is never the only signal for valid/invalid/status/selection.

## 4. Text and scaling

Test Windows text scaling and 100/125/150/175/200% display scaling. Avoid clipped labels and off-screen buttons.

## 5. Motion/transparency

Respect reduced motion and transparency/accessibility settings. Essential state changes remain understandable with animation/blur removed.

## 6. Touch and target sizing

Although desktop mouse/keyboard is primary, hit targets must remain practical for touch and motor accessibility. Do not shrink icon buttons to decorative glyph size.

## 7. Test tooling

Use:

- `winapp ui inspect/search/invoke/screenshot` for automation tree and interaction checks;
- Windows accessibility inspection tools where available;
- keyboard-only manual passes;
- Narrator smoke test;
- High Contrast and text-scaling manual passes.
