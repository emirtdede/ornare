# Screen and State Specifications

## 1. Quick Panel — signature surface

### Window

Target starting footprint: approximately **420 × 330 effective px**, allowed to adapt for localization/text scaling. This is a design target, not a hard fixed size. It must not become a tall mini-dashboard.

No conventional large title bar content. Use custom title-bar integration only if it remains accessible and reliable.

### Content anatomy

Top region:

- folder name, strong but not oversized;
- compact path/location, secondary tone, one line with trimming and tooltip/accessibility full value.

Middle region:

- original Ornare image/drop glyph or standard image glyph;
- `Drop an image` / `Bir görsel bırak`;
- helper: `Click to choose or press Ctrl+V` / `Seçmek için tıkla veya Ctrl+V'ye bas`.

This is not rendered as a permanent card within the window. Negative space establishes the target. A temporary boundary/tint appears on drag-over.

Bottom region:

- `Recent` label only when there are recent items;
- up to ~5 simple icon cells in quick surface; do not create horizontal card containers;
- `Reset` appears only if target has a customization Ornare can safely restore;
- subtle manager/settings affordance.

### Idle state

Calm, no animation loop. The drop action is obvious through location, icon and text.

### Drag-valid state

- subtle accent state;
- optional temporary hairline/dashed or luminous boundary if usability requires it;
- small compositor scale/opacity response;
- text may change to `Release to apply`.

### Drag-invalid

- no shake animation;
- clear concise error state;
- retain current selection context.

### Processing

If <~200 ms, transition directly. If longer, show a small progress/status treatment without blocking animation.

### Success

Replace central copy with quiet success state:

`Icon applied` + `Undo`.

Auto-close after approximately 2.5 seconds unless pointer/focus is in the surface.

### Failure

Use inline error copy and optional `Details`, never a stack of nested dialogs for routine failure.

## 2. Manager — Library

One primary window with Mica foundation if appropriate.

Left navigation: narrow and quiet, not a separate floating card.

Content:

- page title;
- optional search only once library size justifies it;
- continuous grid of icon cells;
- no card background per cell unless selected/hovered;
- empty state has one original illustration/glyph maximum and one concise action.

## 3. Manager — Recent / Favorites

Reuse Library grid presentation. These are filters/collections, not visually separate products.

## 4. Manager — Folders

Continuous list. Suggested columns/fields depending window width:

- icon preview;
- folder name;
- compact path;
- status;
- last updated;
- overflow/context action.

Health state uses text/icon and semantic color, not colored card backgrounds.

Repair action appears contextually for affected rows and optionally as one concise top status line when multiple rows need attention.

For `ExternalIconConflict`, do **not** show generic automatic Repair/Reset as the default. Show a calm status such as `Changed outside Ornare` with explicit actions such as `Reapply Ornare icon` and `Stop tracking`. A reapply re-baselines the live external icon state before taking ownership again.

## 5. Manager — Settings

Sections as typography + spacing:

### Appearance
- Theme: System / Light / Dark (System default)

### Language
- System default / English / Türkçe, only if manual override ships

### Data & diagnostics
- library location/size summary;
- Export diagnostics;
- Repair/reconcile tracked folders where relevant.

### About
- version;
- privacy;
- licenses;
- acknowledgments.

No “settings cards.” Rows may use subtle hover states and separators.

## 6. Dialog policy

Modal dialogs only for destructive/irreversible or legally necessary decisions. Changing an icon is reversible and does not need confirmation. Uninstall customization restore may require explicit installer choice.
