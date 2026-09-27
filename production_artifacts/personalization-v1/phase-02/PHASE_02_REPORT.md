# Phase 2 — Template Model Extension Report

- **Date:** 2026-09-27
- **Status:** **PASS (GATE 2 GREEN)**
- **Artifact:** `production_artifacts/personalization-v1/phase-02/PHASE_02_REPORT.md`

---

## 1. Scope & Objective

Phase 2 extends Ornare's existing F03 template system into a unified personalization data model conforming strictly to Section 14, 15, 16, and 17 of the normative implementation plan:
- Reused F03 architecture without introducing parallel infrastructure.
- Preserved existing stable template IDs (`ornare-classic`, `ornare-soft`, `ornare-flat`).
- Implemented Section 15 template manifest contract (`FolderTemplateManifestEntry` and `FolderTemplateManifest`).
- Established normalized coordinate system for `EmblemSafeZone` and `ReservedBadgeZones`.
- Created unified `FolderEmblemRecipe` and `EmblemKind` supporting None, VectorIcon, and Emoji with mutual exclusivity.
- Extended `RenderRecipe` with emblem parameters and updated `CanonicalRecipeSerializer` with deterministic SHA-256 `render_key` generation and backward-compatible V1 stream deserialization.
- Extended `SemanticSvgRole` and `SemanticSvgRoleExtensions` to support normative roles (`folder-back`, `folder-tab`, `folder-front`, `highlight`, `shadow`, `edge`, `detail`).
- Updated `SvgRecolorService` and `FolderTemplateValidator` to support both `data-role` and `data-ornare-role`.
- Registered all new types in `FolderJsonContext` for zero-reflection Native AOT compatibility.

---

## 2. Models & Contracts Implemented

### `NormalizedRect` (`src/Ornare.Core/NormalizedGeometry.cs`)
- Bounds verification: [0.0, 1.0] for X, Y, Width, Height.
- `Intersects(NormalizedRect other)`: Detects non-overlapping and overlapping zones.
- `Contains(double x, double y)`: Tests point containment.

### `FolderEmblemRecipe` (`src/Ornare.Core/EmblemModels.cs`)
- `Kind`: `EmblemKind` (`None`, `VectorIcon`, `Emoji`).
- `ContentId`: Local asset identifier for vector icons.
- `GraphemeSequence`: Full string sequence preserving multi-codepoint Unicode grapheme clusters (family sequences, skin tones, ZWJ, variation selectors).
- `ForegroundColor`: Dynamic color control (`Auto`, custom hex, white, black).
- `Scale`: Bounded in [0.2, 2.0].
- `OffsetX`, `OffsetY`: Bounded in [-0.5, 0.5].
- Factory methods: `CreateVectorIcon` clears emoji; `CreateEmoji` clears vector icon.

### `FolderTemplateManifestEntry` & `FolderTemplateManifest` (`src/Ornare.Core/TemplateManifestModels.cs`)
- Direct implementation of Section 15 contract:
  - `Id`, `DisplayNameResourceKey`, `Version`, `SvgAssetPath`, `SortOrder`
  - `SupportsColor`, `SupportsEmblem`, `ViewBox`
  - `EmblemSafeZone`: normalized rectangular safe area
  - `ReservedBadgeZones`: normalized exclusion zones for F04/F05 badges
  - `DefaultPalettePolicy`, `RenderPolicyVersion`

### `RenderRecipe` & `CanonicalRecipeSerializer` (`src/Ornare.Core/RenderRecipe.cs`)
- Current recipe version bumped from 1 to 2.
- Binary serializer writes/reads emblem parameters when `recipeVersion >= 2`.
- Binary deserializer safely reads V1 streams with default empty emblem parameters, ensuring existing records deserialize cleanly without migration errors.
- SHA-256 `ComputeRenderKey()` uniquely derives from every visual input (template, version, color, emblem kind, emblem content, emblem color, emblem scale, emblem offset, status/priority, and render policy version).

---

## 3. Verification & Test Evidence

### New Tests in `Ornare.Core.Tests/TemplateModelAndEmblemTests.cs`:
1. `NormalizedRectValidCoordinatesPropertiesSetCorrectly`: PASS
2. `NormalizedRectInvalidCoordinatesThrowsArgumentException`: PASS (multiple invalid boundary cases tested)
3. `NormalizedRectIntersectsDetectsOverlapCorrectly`: PASS
4. `NormalizedRectContainsDetectsPointInside`: PASS
5. `FolderEmblemRecipeNoneHasNoEmblem`: PASS
6. `FolderEmblemRecipeCreateVectorIconSetsFieldsAndClearsEmoji`: PASS
7. `FolderEmblemRecipeCreateEmojiPreservesMultiCodepointGraphemeAndClearsVector`: PASS (tested multi-codepoint ZWJ family sequence `👨‍👩‍👧‍👦`)
8. `RenderRecipeWithEmblemsProducesDistinctRenderKeys`: PASS (proves base vs vector vs emoji keys are strictly distinct)
9. `RenderRecipeCanonicalSerializationRoundTripsEmblemFields`: PASS
10. `SemanticSvgRolesParseNormativeAndLegacyRoles`: PASS
11. `SemanticColorPaletteMapsNormativeRolesCorrectly`: PASS
12. `FolderTemplateManifestNativeAotJsonSerializationSucceeds`: PASS

### Test Suite Execution
- **Ornare.Core.Tests:** 81 passed, 0 failed, 0 skipped.
- **Ornare.Imaging.Tests (Template suite):** 14 passed, 0 failed, 0 skipped.

---

## 4. Phase 2 Sign-Off

Phase 2 is **GREEN**. Ready to proceed to **Phase 3 — Nine Original Shipping Templates**.
