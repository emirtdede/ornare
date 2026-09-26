# F20 UI & Accessibility Matrix (GAP_CLOSURE_F19_F24)

## Overview
This document records the visual quality, theme support, scaling behavior, and accessibility certification for the Manager Multi-Folder Staging Tray introduced in F20.

## Theme & Visual Appearance Matrix
| Theme | Background & Stroke Tokens | Contrast Ratio | Visual Verification |
| :--- | :--- | :--- | :--- |
| **Light Mode** | `SubtleFillColorSecondaryBrush`, `CardStrokeColorDefaultBrush` | > 4.5:1 (WCAG AA) | Subtle gray container border, distinct from main surface, no harsh lines |
| **Dark Mode** | `SubtleFillColorSecondaryBrush`, `CardStrokeColorDefaultBrush` | > 4.5:1 (WCAG AA) | Elevated dark card styling matching Windows 11 design language |
| **System Theme** | Evaluated via `ElementTheme.Default` | Native | Responds dynamically to system light/dark mode changes without restart |
| **High Contrast** | Contrast White / Contrast Black system brushes | > 7:1 (WCAG AAA) | Native system border highlighting, high contrast text and glyphs |

## Display Scaling & Layout Density Matrix
| Scale Factor | Viewport Width | Clipping / Truncation Check | Layout Responsiveness |
| :--- | :--- | :--- | :--- |
| **100% (96 DPI)** | 1024x720 | Zero clipping; badges cleanly visible | Native margins (16,14), compact row height |
| **125% (120 DPI)**| 1024x720 | Zero clipping; labels wrap cleanly | Layout proportions scale linearly |
| **150% (144 DPI)**| 1024x720 | Zero clipping; ellipses on folder paths | TextTrimming="CharacterEllipsis" prevents layout overflow |
| **200% (192 DPI)**| 1024x720 | Zero clipping; clean scroll container | Staged list MaxHeight="200" with auto scrollbars |

## Accessibility & UI Automation
| Requirement | Implementation | Status |
| :--- | :--- | :--- |
| **Keyboard Navigation** | Tab order navigates through DropZone, Staged list items, Rejection pill, Clear Staging button | PASS |
| **Visible Focus** | WinUI 3 default 2px high-visibility focus rectangle active on interactive elements | PASS |
| **AutomationProperties.Name** | `TargetTrayDropZone`: `"Folder Staging Tray"` / `"Klasör Hazırlama Tepsisi"`<br>`ClearStagingButton`: `"Clear staged folders"` / `"Hazırlanan klasörleri temizle"` | PASS |
| **Screen Reader (Narrator)** | Announces drop zone entry, item count upon drop, item names, and rejection reasons | PASS |
| **Reduced Motion / Transparency** | Replaces subtle motion with instant transitions when system setting is active | PASS |

## Functional Staging Scenarios
| Scenario | Expected Behavior | Automated / Manual Evidence |
| :--- | :--- | :--- |
| **Empty State** | DropZone visible; StagedPanel collapsed; zero lingering resources | `StageFoldersSingleValidFolderAcceptedWithIdentity` / Visual inspection |
| **One Folder** | StagedPanel appears; item listed with folder name, path, icon glyph | `StageFoldersSingleValidFolderAcceptedWithIdentity` (PASS) |
| **Multiple Folders (100+)** | All accepted folders virtualized in list; count updated | `StageFoldersOneHundredFoldersAllAccepted` (PASS) |
| **Duplicates** | Deduplicated by canonical path without duplicate badge | `StageFoldersDeduplicatesByCanonicalPath` (PASS) |
| **Already-Managed Folder** | Shows existing badge indicator; does not double-write baseline | `StageFoldersAlreadyCustomizedHasExistingBadge` (PASS) |
| **Invalid / Unsupported Target** | Files, drive roots, non-existent folders rejected with specific error codes | `StageFoldersItemIsFileNotFolderRejectedWithItemIsFileNotFolder` (PASS) |
| **Partial-Valid Multi-Drop** | Valid folders accepted; invalid folders placed in expandable rejection pill | `StageFoldersPartialValidAcceptsValidAndRejectsInvalid` (PASS) |
| **Cancellation** | Active background scanning cancelled cleanly via CancellationTokenSource | `StageFoldersCancellationStopsProcessing` (PASS) |
| **No Persistence from Staging** | Staging folders does NOT create desktop.ini or touch disk | `StageFoldersDoesNotMutateFilesystem` (PASS) |
| **No Nested-Card Regression** | Clean flat single-level container; no multi-nested cards | XAML hierarchy audit (PASS) |
| **Bilingual Localization** | English and Turkish strings for title, subtitle, buttons, badges, errors | Verified in `ManagerWindow.xaml.cs` (PASS) |
