# UX/UI Specification

## 1. North star

Ornare must communicate quality through restraint. The UI should evoke the discipline of a first-party premium Mac utility — clear hierarchy, excellent spacing, quiet motion, direct manipulation, almost no visual noise — while remaining a native Windows application with Windows-native accessibility, input behavior, typography, theming, windowing, and shell integration.

“Apple-like” means **design principles**, never copied assets or pixel-level imitation.

## 2. Structural rule: one surface, not boxes

The default visual strategy is a **continuous surface**. Group content by whitespace, alignment, text hierarchy, dividers, and selection state.

### Forbidden visual patterns

- Cards nested inside cards.
- Panel within panel within another panel.
- Four or more rounded rectangles visible simply to group information.
- Persistent border around the drop target when no drag is active unless usability testing proves it necessary.
- Every setting rendered as its own card.
- Dashboard metric tiles.
- Decorative background geometry.
- Heavy shadows around ordinary content.

A bounded container must have a semantic reason: focus, selection, transient interaction, scannable collection cell, destructive confirmation, or native platform behavior.

## 3. Quick panel

### 3.1 Purpose

The quick panel exists for one job: assign an image to the selected folder with minimal thought.

### 3.2 Information order

1. selected folder name;
2. path or compact location context;
3. central image action;
4. secondary recent/favorite shortcuts if they fit without visual crowding;
5. Reset only when the folder is currently customized;
6. settings/manager entry as a low-emphasis affordance.

### 3.3 Visual composition

Do not create a title-card + drop-card + recents-card stack.

Preferred composition:

- one window surface;
- title text at top with compact path below;
- generous negative space in the middle containing image glyph + “Drop an image” and low-emphasis “Click or Ctrl+V”;
- recent icons as simple image cells on the same plane;
- one subtle divider only where needed.

During drag-over, the central region may gain a subtle accent tint, scale, or temporary outline to communicate that a drop is valid. Remove the treatment immediately when drag leaves.

### 3.4 Interaction states

**Idle** — calm, no flashing call-to-action.

**Drag enter / valid** — drop region subtly brightens; icon/text shifts to accent; compositor scale ≤ ~1.015; no pulsing.

**Drag invalid** — do not animate aggressively. Communicate unsupported input with a clear icon/text state.

**Processing** — avoid full-screen spinner. If action exceeds perceptible immediacy, use a compact progress affordance near the central action.

**Success** — content transitions to a quiet check state and `Icon applied · Undo`; close after ~2.5 seconds if safe.

**Failure** — keep panel open; show actionable short message and optional Details disclosure. Never lose the selected folder context.

### 3.5 Window behavior

- Open on the invoking monitor.
- Position near the invocation/cursor context without covering critical system UI.
- Clamp to work area.
- Correct for mixed-DPI monitors.
- Avoid stealing focus in surprising ways, but accept keyboard/paste immediately when shown.
- Escape closes.
- Tab navigation is deterministic.

## 4. Full manager

### 4.1 Navigation

Use a simple left navigation/sidebar only if it reduces complexity. Keep hierarchy shallow. Target sections:

- Library
- Recent
- Favorites
- Folders
- Settings

Repair status belongs within Folders or a contextual status area, not a dashboard tile.

### 4.2 Library

- Icons displayed as clean grid cells.
- No card chrome around each item by default.
- Hover/selection uses subtle background or scale/opacity state.
- Context actions appear only on hover/focus/context menu.
- Empty state is concise and directs user to add/use an image.

### 4.3 Customized folders

Prefer a list/table-like continuous surface:

- folder icon;
- folder name/path;
- status;
- last changed;
- contextual actions.

Rows separated by spacing or a thin separator. Do not wrap each row in a floating card.

### 4.4 Settings

Flat setting rows, grouped with section heading + spacing. Use native toggles/segmented controls where semantically correct. Do not place every setting in a large rounded container.

## 5. Apple-inspired principles adopted

- Content-first branding.
- Accent color used sparingly.
- Minimal typefaces and clear typographic hierarchy.
- Materials create hierarchy, not decoration.
- Drag and drop receives clear feedback.
- Familiar platform interactions remain familiar.
- Avoid deeply nested navigation.
- Reduce visual complexity instead of adding ornamental containers.

## 6. Apple-specific assets explicitly prohibited

Do not use or ship:

- Apple Design Resources/Figma kits;
- SF Symbols;
- SF Pro font files;
- macOS screenshots as product assets;
- copied Apple system glyphs;
- traced Apple UI controls.

Apple's Design Resources license limits those resources to mockups for software running on Apple operating systems. Ornare is a Windows product and must use original/native Windows resources.

## 7. Windows-native adaptation

- Typography: Segoe UI Variable/system UI font.
- Standard action glyphs: Fluent System Icons where licensed/appropriate.
- Signature product icons: original SVG designed for Ornare.
- Manager base: Mica when it improves hierarchy/performance and contrast.
- Quick transient surface: Desktop Acrylic where validated; fall back gracefully.
- Do not stack Acrylic/Mica across nested content regions.
- Use ThemeResource and system/high-contrast resources.

## 8. Motion

Motion is functional. Default duration ranges should be approximately 120–220 ms; use longer only when semantic continuity requires it.

Preferred properties:

- opacity;
- translation;
- scale;
- compositor-supported reveal/state transitions.

Avoid layout-heavy animation. Respect reduced motion. Never make users wait for animation before acting.

## 9. Copy tone

Plain, short, professional.

Good:

- `Drop an image`
- `Click to choose or press Ctrl+V`
- `Icon applied`
- `Undo`
- `This folder is protected by Windows.`

Avoid marketing tone inside operational UI.

## 10. Design review gate

A screen fails review if any reviewer can reasonably describe it as:

- “a SaaS dashboard”;
- “cards inside cards”;
- “generic AI-generated UI”;
- “glass everywhere”;
- “macOS clone”;
- “Windows Settings clone”.

It passes only when hierarchy remains clear even if borders and shadows are mentally removed.
