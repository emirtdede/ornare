# Phase 4 Report: Staged Personalization State

## 1. Executive Summary
Phase 4 implements the authoritative state staging architecture for the Ornare Quick Panel, strictly enforcing the product separation between **Mode A** (Custom Image Icon) and **Mode B** (Folder Personalization), staged-only editing semantics, and explicit Apply / Discard rules per Section 3 and Section 5 of the Normative Plan.

---

## 2. Core Architecture & Components
- **`PersonalizationMode`**: Enum identifying `CustomImage` (Mode A) and `FolderTemplate` (Mode B).
- **`CustomImageStagingState`**: Immutable record encapsulating user image bytes, source paths, image placement (Contain/Cover/Fill), padding percentage (0..30%), and background mode/color.
- **`FolderTemplateStagingState`**: Immutable record encapsulating selected template ID & version, folder color, center emblem (`FolderEmblemRecipe`), and optional status/priority badges.
- **`PersonalizationStagingModel`**: State machine maintaining both staged state and committed baseline state:
  - **Memory Separation**: Retains staged values for both modes concurrently; switching modes preserves staged values in memory without writing to disk.
  - **Staged-Only Mutations**: Editing template, color, emblem, or image updates only in-memory staged properties. Zero disk/registry/shell mutations occur during exploration.
  - **`CanApply` Gating**: Evaluates to `true` if and only if the staged state for the active mode differs from the committed baseline and has valid content.
  - **`Discard()`**: Reverts the active mode and staged state to the exact committed baseline.
  - **`Commit()`**: Updates the baseline to the current staged state upon successful durable application.
  - **Mutual Exclusivity**: Vector emblem and emoji are strictly mutually exclusive. Staging one clears the other.
  - **`BuildRenderRecipe()`**: Deterministically produces the canonical `RenderRecipe` for the active mode.

---

## 3. Native AOT Registration
All new staging types (`PersonalizationMode`, `CustomImageStagingState`, `FolderTemplateStagingState`) are registered in `FolderJsonContext.cs` ensuring full Native AOT reflection-free serialization compatibility.

---

## 4. Verification Evidence
- `PersonalizationStagingTests`: 11 targeted unit tests verifying:
  - `CanApply` initial state (`false` when staged == committed)
  - Mode B template, color, vector emblem, and emoji staging
  - Mutual exclusivity between vector icon and emoji
  - Removal of emblem returning `Kind = None`
  - Mode A custom image staging and placement updates
  - Memory retention across mode switches
  - Discard semantics restoring baseline
  - Commit semantics updating baseline
  - Deterministic recipe construction for both modes
- Total passing tests:
  - `Ornare.Core.Tests`: 92 passed, 0 failed.
  - `Ornare.Imaging.Tests`: 203 passed, 0 failed.
  - `Ornare.Storage.Tests`: 54 passed, 0 failed.
  - `Ornare.UiTests`: 16 passed, 0 failed.
  - Total passing: 365+ tests.

Phase 4 is complete and ready to commit.
