# Preflight Check: F14 — PNG Export

**Feature ID:** F14  
**Feature Name:** PNG Export  
**Execution Order:** 14 of 27  
**Traceability ID:** `ORN-EXP-F14`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Specification:** `feature-expansion/features/F14_png-export.md`  
**Date:** 2026-09-26  

---

## 1. Scope & Objective

Export any selected asset or rendered recipe as a high-resolution transparent or solid-background PNG (`.png`) for reuse outside Ornare.
The export pipeline must:
1. Expose exactly three standard sizes: `256×256`, `512×512`, and `1024×1024`, with `1024×1024` as the default.
2. Render on-demand only for the requested size; never pre-render all three sizes.
3. Render directly from normalized source/vector recipe at the target resolution (no upscaling of a 256px ICO frame).
4. Accurately encode alpha channels when the recipe background is transparent, and opaque colors when solid background is specified.
5. Maximize repeat export performance by retrieving from the F18 render cache on exact cache hits (`TargetFormat = "png"`, `TargetFrameSizes = [targetSize]`).
6. Guarantee strict destination atomicity on physical Windows filesystems using `AtomicExportWriter`:
   - Same-directory sibling temporary file (`.ornare-export-{GUID}.tmp`) with `FileMode.CreateNew`.
   - `Flush(flushToDisk: true)` before validation.
   - Pre-commit container and dimension validation (`ValidatePngBasic`).
   - Atomic replacement via `ReplaceFileW` for existing destinations and `MoveFileExW(MOVEFILE_WRITE_THROUGH)` for new destinations.
   - Return `ErrorCode.UnsupportedExportDestination` if atomic filesystem semantics are not supported.
   - Zero corrupted destination files on failure; pre-existing files preserved byte-for-byte.
7. Cancel in `FileSavePicker` performs zero writes, creates zero temp files, and mutates zero SQLite tables.
8. Exported paths are never registered in the internal `LibraryAsset` database.

---

## 2. Invariants & Security Boundaries

1. **Size Support (Fixed Decision 1):**
   - Exactly `256×256`, `512×512`, and `1024×1024`.
   - Default is `1024×1024`.
   - Invalid sizes rejected with `ErrorCode.InvalidTarget`.
2. **On-Demand Vector Rendering (Fixed Decision 2 & Risk Mitigations):**
   - SVGs and template vectors are rasterized directly at the target resolution (256, 512, 1024).
   - Status badges are composited from vector paths at target resolution for sizes > 256px to prevent blurriness.
   - Bitmaps are bounded and disposed immediately after encoding to prevent memory spikes.
3. **Alpha Preservation (Implementation Sequence 3):**
   - Transparent background recipes yield PNG images with an active alpha channel (`image.HasAlpha == true`).
   - Solid background recipes yield opaque PNG images with the exact hex color.
4. **Cache Key Determinism (Implementation Sequence 1):**
   - Cache key includes `TargetFormat = "png"` and `TargetFrameSizes = [targetSize]`, ensuring distinct keys for 256, 512, and 1024.
5. **Atomic Commit & Safe Temp Handling (Fixed Decision 4):**
   - Temporary file named `.ornare-export-{GUID}.tmp` in destination directory.
   - Stale temps older than 24h cleaned up before write.
   - Atomic replacement preserves prior destination on failure.
6. **No System.Drawing / GDI+ (Forbidden Implementations):**
   - Magick.NET only for PNG rasterization and encoding.

---

## 3. Test Strategy & Plan

- Unit & integration tests in `tests/Ornare.IntegrationTests/PngExportIntegrationTests.cs`:
  - `ExportPng_SupportedSizes_ProduceExactDimensionsAndAlpha`: 256, 512, and 1024 PNG exports verify exact dimensions and alpha presence.
  - `ExportPng_SolidBackground_ProducesOpaquePng`: Solid color recipe produces valid PNG without transparency.
  - `ExportPng_ExistingDestination_ReplacedAtomically`: Atomic replacement overwrites existing file safely.
  - `ExportPng_InjectedFailure_PreservesOriginalDestination`: Failure during export leaves original destination untouched.
  - `ExportPng_CacheHit_ReusesCachedArtifactWithoutReEncoding`: Exact cache hit returns cached PNG artifact.
  - `ExportPng_InvalidSize_FailsWithInvalidTarget`: Unsupported sizes (e.g. 128, 48) return `ErrorCode.InvalidTarget`.
  - `ExportPng_Cancel_ProducesZeroFiles`: Cancelation results in zero files created.
- Full solution build and test pass (`dotnet test --configuration Release`).
- Managed ARM64 build pass (`pwsh -File scripts/build-managed-arm64.ps1`).
- Real physical golden path pass (`pwsh -File scripts/test-real-golden-path.ps1`).
