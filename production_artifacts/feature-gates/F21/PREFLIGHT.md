# Preflight Check: F21 — Apply Last Icon

**Feature ID:** F21  
**Feature Name:** Apply Last Icon  
**Execution Order:** 20 of 27  
**Traceability ID:** `ORN-EXP-F21`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Specification:** `feature-expansion/features/F21_apply-last-icon.md`  
**Date:** 2026-09-26  

---

## 1. Scope & Objective

Enable users to apply the most recently durably committed Ornare icon render recipe to another eligible folder directly from File Explorer or app activation, without loading Storage or Imaging code inside File Explorer.

The feature subsystem must:
1. **Persistent Pointer `LastAppliedRenderKey`:**
   - Updated ONLY in the same durable post-filesystem database commit that records a successful apply; never on preview, import, render-only export, or failed/canceled apply operations.
2. **Reference-Safe Cleanup Retention:**
   - The referenced immutable recipe and all source assets required to reconstruct it must participate in reachability analysis and be protected from deletion while `LastAppliedRenderKey` points to them.
3. **Decoupled Shell Extension:**
   - Explorer shell extension remains tiny, native C++ with zero dependencies on SQLite, Imaging, or WinUI.
   - Enumerate two top-level context menu entries in File Explorer:
     - Existing **Set Folder Icon** (golden-path command).
     - Separate **Ornare** app-attributed flyout supplying secondary commands via `IExplorerCommand::EnumSubCommands`.
   - In F21, the `Ornare` flyout contains ONLY the implemented child **Apply last icon** (`"Son simgeyi uygula"` in Turkish). No disabled or placeholder future children.
   - Shell extension sends only versioned activation: `ornare://apply-last?v=1&path=<url_encoded_path>` (or CLI `--apply-last "<folder>"`).
4. **App-Side Apply Execution & Resilient Cache Rebuild:**
   - App receives activation, validates target folder eligibility via `TargetFolderValidator`.
   - Resolves `LastAppliedRenderKey`. If no pointer exists or if pointer is corrupt/unresolvable, transactionally clears the pointer and returns typed `ErrorCode.NoLastAppliedRecipe`.
   - If canonical ICO exists in cache, reuses it immediately. If derived ICO is missing from cache but canonical recipe inputs remain, rebuilds the ICO deterministically through normal renderer.
   - Executes standard durable apply transaction (baseline snapshot, journal entry, `.ornare\folder.ico` write, `desktop.ini` merge, database assignment, NTFS identity capture, history record, shell notification).
5. **Serialization:**
   - Rapid concurrent activations on the same folder are serialized cleanly without race conditions.

---

## 2. Invariants & Risk Mitigations

| Invariant / Risk | Specification Rule | Planned Mitigation |
|---|---|---|
| `F21-DEC-01` | Persistent pointer `LastAppliedRenderKey` | Stored in `settings` table; updated only after durable apply transaction completes. |
| `F21-DEC-02` | Retention in cleanup | F18 cleanup query already checks `settings WHERE key = 'LastAppliedRenderKey'`; verify recipe and source asset cannot be deleted while referenced. |
| `F21-DEC-03` | No update on preview/failure | Apply Last pointer is set ONLY on successful apply commit. Preview/import/failure/reset do not touch it. |
| `F21-DEC-04` | Shell extension isolation | Zero SQLite/Imaging/WinUI in Explorer DLL. Shell only builds activation URI or CLI parameters. |
| `F21-DEC-05` | Two top-level app entries | Top-level "Set Folder Icon" + top-level "Ornare" flyout (`ECF_HASSUBCOMMANDS`). |
| `F21-DEC-06` | Single child in F21 | `IEnumExplorerCommand` yields only `OrnareApplyLastCommand`. Zero placeholder children. |
| `F21-DEC-07` | Menu performance (<10 ms) | All menu-construction methods (`GetTitle`, `GetState`, `GetFlags`, `EnumSubCommands`) do static non-blocking checks only. |
| `F21-DEC-08` | Cache missing recovery | Rebuild ICO from retained immutable recipe JSON if cache file was deleted. |
| `F21-DEC-09` | Corrupt pointer handling | If pointer cannot be resolved, transactionally clear and return `ErrorCode.NoLastAppliedRecipe`. |

---

## 3. Test Strategy & Plan

1. **Storage & Recipe Retention Tests (`tests/Ornare.Storage.Tests/`):**
   - No Last Applied state returns null.
   - Setting `LastAppliedRenderKey` roundtrip and transactional clear.
   - Verify `CleanupUnreferencedAssetsAsync()` preserves source assets and recipe when referenced by `LastAppliedRenderKey`.
2. **Apply Last Engine & Integration Tests (`tests/Ornare.IntegrationTests/`):**
   - Apply Last on folder without prior apply returns `NoLastAppliedRecipe`.
   - Apply icon on Folder A -> Apply Last on Folder B: verifies Folder B receives exact same icon semantics and identity.
   - Rebuilding missing cache file: delete cached ICO file -> verify Apply Last rebuilds ICO from stored recipe.
   - Corrupt pointer handling: set bogus key -> verify graceful failure, pointer cleared, returns `NoLastAppliedRecipe`.
   - Target validation: verify file, non-existent, UNC, or reparse point targets are rejected.
3. **Shell Extension Tests:**
   - Verify `CLSID_OrnareFlyoutCommand` implements `IExplorerCommand` with `ECF_HASSUBCOMMANDS`.
   - Verify `EnumSubCommands` returns `IEnumExplorerCommand` yielding exactly 1 item ("Apply last icon").
   - Verify latency budget (< 10 ms).
4. **Full Verification:**
   - `dotnet test --configuration Release` (100% green).
   - `pwsh -File scripts/build-managed-arm64.ps1` (100% green).
   - `pwsh -File scripts/test-real-golden-path.ps1` (100% green).
