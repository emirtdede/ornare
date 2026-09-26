# F18 Preflight Note — Asset Deduplication

**Feature:** F18 — Asset Deduplication  
**Date:** 2026-09-26  
**Status:** Preflight Complete  

---

## 1. Expected vs Actual Document Map
| Expected Path | Actual Path | Status |
|---|---|---|
| `docs/LOGIC_INVARIANTS.md` | `docs/LOGIC_INVARIANTS.md` | Exact match |
| `docs/ARCHITECTURE.md` | `docs/ARCHITECTURE.md` | Exact match |
| `docs/QUALITY_GATES.md` | `docs/QUALITY_GATES.md` | Exact match |
| `docs/IMAGE_PIPELINE.md` | `docs/IMAGE_PIPELINE.md` | Exact match |
| `docs/STORAGE_DATA_MODEL.md` | `docs/STORAGE_DATA_MODEL.md` | Exact match |
| `docs/FOLDER_CUSTOMIZATION.md` | `docs/FOLDER_CUSTOMIZATION.md` | Exact match |
| `docs/SHELL_EXTENSION.md` | `docs/SHELL_EXTENSION.md` | Exact match |
| `docs/PERFORMANCE.md` | `docs/PERFORMANCE.md` | Exact match |
| `docs/TARGET_ELIGIBILITY.md` | `docs/TARGET_ELIGIBILITY.md` | Exact match |
| `docs/LOCALIZATION.md` | `docs/LOCALIZATION.md` | Exact match |
| `docs/TEST_PLAN.md` | `docs/TEST_PLAN.md` | Exact match |

All 11 required foundational documents are verified present and read in order.

---

## 2. Live Implementation Points & Affected Projects/Files

### `Ornare.Core`
- Introduce `RenderRecipe`: immutable record containing all output-affecting parameters with deterministic canonical serialization (`ToCanonicalBytes()`) and `ComputeRenderKey()`.
- Introduce `CanonicalRecipeSerializer`: deterministic binary/byte-level serializer using fixed field order, length-prefixed strings, fixed endianness, invariant representation.
- Add golden serialization byte fixtures.

### `Ornare.Storage`
- `SqliteStorageService.cs`:
  - Forward versioned schema migration (UserVersion / PRAGMA user_version).
  - Add `render_cache` table with unique constraint on `render_key`.
  - Add `render_key` columns where applicable.
  - Implement `GetRenderCacheEntryAsync(renderKey)`, `SaveRenderCacheEntryAsync(entry)`, `InvalidateRenderCacheAsync(renderKey)`.
  - Reference-safe cleanup: query references from `folder_assignments`, `history`, `favorites`, `settings (LastAppliedRenderKey)`.
- `IStorageService.cs`: expose render cache and streaming asset dedup contracts.

### `Ornare.Imaging`
- `ImagePipeline.cs`:
  - Streaming SHA-256 calculation for source files (`ComputeSourceHashStreamAsync` / `ComputeSourceHash`).
  - Pre-render cache check: before executing decode + composite + multi-size Lanczos downsampling, inspect render cache for `RenderKey`.
  - Cache validation: verify file exists, size matches, ICO magic header `00 00 01 00` valid. If corrupt, invalidate and re-render.
  - Cache miss atomic publishing: write to temporary unique file, flush, atomically publish to `library/renders/<RenderKey>.ico`. Under race condition, loser deletes temp and reuses winner.

### `Ornare.App`
- QuickPanel and Manager use `RenderKey`-aware pipeline for immediate cache hits on previously rendered recipes.

---

## 3. Affected Invariants
- **INV-FS-001**: Ornare never deletes or overwrites a file it cannot prove it owns.
- **INV-FS-004**: Source-image lifetime is irrelevant after successful Apply.
- **INV-TX-004**: Cleanup is always last and only removes proven-unreferenced Ornare-owned artifacts.
- **INV-CON-003**: Duplicate rapid invocation is safe and cannot duplicate/desynchronize history or assets.
- **INV-IMG-001**: No supported image input can bypass configured resource limits.
- **INV-IMG-003**: Every ICO size is rendered from normalized source, not recursively from a smaller generated frame.

---

## 4. Schema & Package Implications
- SQLite schema forward migration to version 2 (creating `render_cache` table and indexes).
- No new package dependencies. System `winsqlite3` and `Microsoft.Data.Sqlite.Core` retained.
- Package manifest: No new capability or manifest changes required for F18.

---

## 5. OSS Decisions
- Reusing Magick.NET for image operations.
- Built-in `System.Security.Cryptography.SHA256` for streaming SHA-256.
- No perceptual hashes (strictly forbidden by specification).

---

## 6. Measurable Risks & Mitigations
1. **Recipe Key Collision / Visual Drift**: Any output change must affect `RenderKey`.
   *Mitigation:* Canonical recipe includes all 16 specified fields with explicit versions + golden byte fixture tests.
2. **Concurrent Duplicate Import / Render Race**:
   *Mitigation:* SQLite unique constraints + atomic file move/replace with race exception handling where loser drops temp and reuses existing file.
3. **Corrupt Cache Utilization**:
   *Mitigation:* Validation of cached file header, size, and readable ICO directory before accepting hit.

---

## 7. Baseline Performance Capture
- Complete test suite passing: 89 passed, 0 failed.
- Render-stage cache miss (cold ICO multi-frame render): ~35-85 ms depending on image dimensions.
- Target cache hit: ≤5 ms (pure metadata + file validation, 0 decode, 0 encode).
