---
description: Perform strict premium UI/UX review on changed Ornare surfaces.
---
# /ui-review

For every affected screen/state:

1. run the actual app in Light and Dark;
2. capture screenshots with `winapp ui screenshot` or equivalent;
3. inspect UIA tree;
4. check keyboard navigation/focus;
5. switch tr-TR and inspect clipping/hierarchy;
6. inspect representative 100/150/200% DPI; mixed-DPI if window placement changed;
7. inspect High Contrast;
8. check reduced-motion behavior if animations changed;
9. explicitly count unnecessary containers/borders/shadows;
10. compare with `UX_UI_SPEC.md` and `DESIGN_SYSTEM.md`.

Record the result in `production_artifacts/design/ui-review-<surface>.md`. Fail the review if it looks like a card dashboard, generic AI UI, macOS clone, or overuses glass/accent. Fix issues before declaring UI complete.
