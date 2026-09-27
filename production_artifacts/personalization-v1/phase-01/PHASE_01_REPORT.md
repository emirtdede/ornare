# Phase 1 — Preflight & Asset Provenance Report

- **Date:** 2026-09-27
- **Commit Baseline:** `12657149b2a3164986b324850a4b203ac8361964`
- **Status:** **PASS (GATE 1 GREEN)**
- **Artifact:** `production_artifacts/personalization-v1/phase-01/PHASE_01_REPORT.md`

---

## 1. Audit Summary & Preflight Execution

The mandatory preflight (`production_artifacts/personalization-v1/PREFLIGHT.md`) was executed prior to any production code modifications.
Every project boundary, invariant, storage schema state (V1-V6), and performance baseline was audited.

### Automated Test Baseline
A complete, clean run of `dotnet test` was conducted on the clean working tree:
- **Ornare.Core.Tests:** 64 passed, 0 failed, 0 skipped
- **Ornare.Storage.Tests:** 54 passed, 0 failed, 0 skipped
- **Ornare.Imaging.Tests:** 179 passed, 0 failed, 0 skipped
- **Ornare.IntegrationTests:** 120 passed, 0 failed, 0 skipped
- **Ornare.UiTests:** 16 passed, 0 failed, 0 skipped
- **Total:** 433 tests passed, 0 failed, 0 skipped (Duration: ~2m 11s).

---

## 2. Legacy Asset Decoupling (`Windows_11_FOLDER.svg`)

- **Finding:** `assets/Windows_11_FOLDER.svg` had unclear redistribution rights. Commit `2f576e5` embedded its contents into `OrnareClassicTemplate.cs` and a unit test in `FolderTemplateTests.cs`.
- **Action Taken:**
  1. Relocated `assets/Windows_11_FOLDER.svg` to `assets-references/legacy/Windows_11_FOLDER.svg` using `git mv` to preserve git commit lineage.
  2. SHA-256 Hash verified: `6F00228283D5EA7554A93741E41FF193E37210CD52C8150F0A3572CB8491A215`.
  3. Created `docs/design-references/folder-style-sources.md` recording provenance and licensing status.
  4. Packaging rules in `scripts/build-rc-candidate.ps1` and `scripts/create-package.ps1` verified: `assets-references/` is strictly outside package layout and will not be included in MSIX.

---

## 3. Existing F03 Template System Audit

- Live IDs verified in `src/Ornare.Imaging/Templates/`:
  - `ornare-classic` (Version: 1)
  - `ornare-soft` (Version: 1)
  - `ornare-flat` (Version: 1)
- Policy:
  - Preserve live IDs (`ornare-classic`, `ornare-soft`, `ornare-flat`).
  - To isolate cache and ensure clean blank-canvas geometry without affecting existing on-disk custom icons, existing templates will have their version bumped to `2`.
  - The new 6 templates (`ornare-glass`, `ornare-aqua`, `ornare-grove`, `ornare-air`, `ornare-flow`, `ornare-geo`) will start at version `1`.

---

## 4. Emblem Library Selection & Licensing Audit

- **Requirement:** 320 curated monochrome vector icons across 12 categories.
- **Evaluation:**
  - Tabler Icons: MIT License, highly consistent 24x24 grid, perfect for center emblem scaling.
  - Microsoft Fluent UI System Icons (`@fluentui/svg-icons`): MIT License.
  - Lucide: ISC License.
- **Selection:** Tabler Icons / Fluent Icons verified permissive MIT licenses. Curated 320 SVG assets will be packaged under `assets/emblems/` with `manifest.json` and explicit `THIRD-PARTY-NOTICES.md` records.

---

## 5. Blank-Canvas Originality Declaration

Every production folder SVG asset will be authored from a blank canvas on a 256×256 viewBox, exposing standard semantic roles:
- `data-role="folder-back"`
- `data-role="folder-tab"`
- `data-role="folder-front"`
- `data-role="highlight"`
- `data-role="shadow"`
- `data-role="edge"`
- `data-role="detail"` (optional)

No copying, tracing, or vectorization of Windows, Apple, GNOME, KDE, or Google assets is permitted.

---

## 6. Phase 1 Sign-Off

Phase 1 is **GREEN**. Ready to proceed to **Phase 2 — Template Model Extension**.
