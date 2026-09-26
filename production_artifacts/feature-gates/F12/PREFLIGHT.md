# Preflight Check: F12 — ICO Import

**Feature ID:** F12  
**Feature Name:** ICO Import  
**Execution Order:** 12 of 27  
**Traceability ID:** `ORN-EXP-F12`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Specification:** `feature-expansion/features/F12_ico-import.md`  
**Date:** 2026-09-26  

---

## 1. Scope & Objective

Safely import existing `.ico` files into Ornare. Compliant multi-frame icons containing all 10 Ornare sizes (`16, 20, 24, 32, 40, 48, 64, 96, 128, 256`) pass through the **fast-path** without re-encoding, preserving hand-crafted pixel precision bit-for-bit. Non-compliant or single-frame ICOs are normalized using deterministic frame selection (exact match first, then smallest larger, then largest) rendered independently without recursive downsampling.

---

## 2. Invariants & Security Boundaries

1. **Parser & Hostile Input Safety:**
   - File extension alone is never trusted.
   - Header validation enforces magic `0x00 0x00 0x01 0x00`.
   - Maximum frame count limit: $\le 64$.
   - Maximum file size: $\le 20\text{ MB}$.
   - Decoded memory limit: $\le 50\text{ MB}$.
   - Truncated headers and directory entries extending beyond file length are rejected safely before full decoding.
2. **Fast-Path Criteria (Fixed Decision 2):**
   - Decodes successfully without errors.
   - Every frame in the container is square (`width == height`).
   - Dimensions are within `1..256`.
   - Contains square frames for **every** required size: `16, 20, 24, 32, 40, 48, 64, 96, 128, 256`. Extra valid frames are allowed.
   - Original bytes stored directly with `ImportKind = IcoCompliant`.
3. **Deterministic Normalization (Fixed Decision 3):**
   - For missing sizes, choose source deterministically:
     1. Exact-size valid frame first.
     2. Smallest valid frame whose dimension is greater than the target.
     3. Largest valid frame.
   - Render each target independently from that chosen source frame using Lanczos resampling; never resize from a previously generated target frame.
   - Store canonical imported asset by SHA-256; derived normalized ICO has separate render key (`NormalizationPolicyVersion = 2`).
4. **Folder Isolation:**
   - Applying an imported ICO always writes a folder-owned copy to `.ornare\folder.ico`. It never points `desktop.ini` to an external source path.

---

## 3. Test Strategy & Plan

- **Hostile Corpus Tests:**
  - Zero-length / truncated headers (< 22 bytes).
  - Invalid magic bytes (PNG, JPEG, CUR, text).
  - Header frame count = 0 or frame count > 64.
  - Directory entries with offsets or lengths exceeding file size.
  - Frame dimensions exceeding 256x256.
- **Fast Path Verification:**
  - A 10-frame compliant ICO triggers fast-path: `IsFastPath == true`, `ImportKind == IcoCompliant`.
  - Bit-for-bit file equality between source and canonical output.
- **Normalization Verification:**
  - Single-frame 256x256 ICO normalizes to all 10 standard sizes.
  - 4-frame standard ICO (16, 32, 48, 256) normalizes to 10 sizes, preserving exact frames for 16, 32, 48, 256 and choosing smallest larger for 20, 24, 40, etc.
- **Bounded Preview Decode:**
  - Generates 256x256 PNG preview without full multi-frame transcoding.
- **Deduplication:**
  - Re-importing the same ICO yields the identical canonical hash and render key.
- **Architecture Gates:**
  - Managed ARM64 Release build clean (0 errors, 0 warnings).
  - Full test suite green.
