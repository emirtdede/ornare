# Ornare Design System Specification

## 1. Objective

Create a reusable native WinUI design system that produces a distinctive premium utility without replacing Windows semantics. It should be reusable across future Vellium Windows utilities, but every abstraction must earn its existence.

Suggested project: `Ornare.DesignSystem`.

## 2. Design tokens

Centralize semantic tokens, not arbitrary visual constants.

### Color

Prefer semantic resources:

- `Surface.Base`
- `Surface.Transient`
- `Surface.Hover`
- `Surface.Selected`
- `Text.Primary`
- `Text.Secondary`
- `Text.Disabled`
- `Border.Subtle`
- `Accent.Primary`
- `Status.Success`
- `Status.Warning`
- `Status.Error`

Every custom color requires Light, Dark, and High Contrast behavior. Never use color as the sole state indicator.

### Typography

Use Segoe UI Variable/system resources. Hierarchy comes from size, weight, spacing, and tone. Avoid thin/light weights at small sizes.

Suggested semantic roles, to be validated against Windows scale:

- Display: rare, manager empty state only if needed.
- Title: selected folder/window context.
- Heading: page/section title.
- Body: primary reading text.
- Caption: path, metadata, helper text.

Use as few roles as possible.

### Spacing

Use a small coherent scale. Recommended baseline values to validate visually:

`4, 8, 12, 16, 20, 24, 32, 40` effective pixels.

Avoid arbitrary values in individual screens unless optical correction requires them.

### Radius

Use modest radii and fewer containers. Radius is not a substitute for hierarchy. Define Small/Medium/Large only if actually used.

### Elevation/shadow

Default content has no shadow. Use elevation only for actual floating/transient surfaces, menus, and modal separation.

## 3. Components

Build only components with repeated semantic behavior. Candidate set:

- `OrnareButton`
- `OrnareIconButton`
- `OrnareDropTarget`
- `OrnareToastHost`
- `OrnareIconCell`
- `OrnareEmptyState`
- `OrnareStatusText`
- `OrnareSectionHeader`
- `OrnareSettingRow`

Do not create wrapper components solely to enforce a rounded card around arbitrary content.

## 4. Drop target

The drop target should mostly be **negative space**, not a permanent framed card. In idle state it may have no visible border. During drag-over it gains a temporary visual boundary/tint and semantic status.

Required states:

- idle;
- pointer hover;
- keyboard focus;
- drag valid;
- drag invalid;
- processing;
- success;
- error;
- disabled.

Its UI Automation name/instructions must make the action understandable without vision.

## 5. Icon cells

Library/recent/favorite icon cells should show the asset at a useful size with minimal chrome. Hover/focus reveals context actions. Selection is visible in both color and shape/outline/indicator.

## 6. Toast/inline feedback

Prefer inline/transient feedback inside the active surface over OS toast notifications for immediate operations.

Example:

`✓ Icon applied    Undo`

Do not stack many notifications. This utility should rarely generate simultaneous messages.

## 7. Materials

Use Windows system backdrop materials semantically:

- Mica: foundation for primary manager window where appropriate.
- Desktop Acrylic: transient/light-dismiss surface such as the quick panel, only if contrast/performance is good.
- No material-on-material nesting for decorative effect.

Apple HIG's current material guidance is used only as a principle: translucent material belongs to functional/navigation layers and should be used sparingly rather than repeated throughout content.

## 8. Motion tokens

Define semantic motion rather than bespoke timing everywhere:

- `Motion.Fast` ~120 ms
- `Motion.Standard` ~160-180 ms
- `Motion.Emphasis` ~200-220 ms

Easing should feel responsive, not bouncy. Reduce/disable nonessential motion when Windows accessibility preferences request it.

## 9. Iconography

Standard actions may use Microsoft Fluent System Icons under their applicable license. Product-specific concepts use original SVG assets.

Rules:

- no emoji;
- no SF Symbols;
- no random mixture of icon families;
- preserve crisp rendering at small sizes;
- provide accessible names separately from glyphs.

## 10. Anti-pattern checklist

Reject a design revision when it introduces:

- more containers instead of clearer grouping;
- a border around every region;
- decorative gradients to make an empty screen “interesting”;
- typography that relies on ultralight weights;
- controls whose hit target is smaller than practical desktop/touch accessibility norms;
- hover-only access to an essential command without keyboard equivalent;
- accent color on most controls;
- custom behavior that breaks standard keyboard/mouse expectations.
