# Feature Gate Preflight: F24 — CLI + Machine-Readable Output

**Feature ID:** F24  
**Feature Name:** CLI + Machine-Readable Output  
**Specification:** `feature-expansion/features/F24_cli-json.md`  
**Execution Order:** 23 of 27  
**Traceability ID:** `ORN-EXP-F24`  
**Parent Contract:** `feature-expansion/00_SHARED_ARCHITECTURE_CONTRACT.md`  
**Date:** 2026-09-26  

---

## 1. Scope & Objective

Add a production Native-AOT-friendly CLI console executable `Ornare.Cli` (`ornare.exe`) that executes identical Core operations as the WinUI application, exposing deterministic command grammar, strict option matrices, structured exit codes, and locale-neutral source-generated JSON contracts without initializing WinUI.

- **Project:** `src/Ornare.Cli/Ornare.Cli.csproj`
- **TFM:** `net10.0-windows10.0.26100.0`
- **Allowed Dependencies:** `Ornare.Core`, `Ornare.Imaging`, `Ornare.Storage`, `Ornare.Windows`, `System.CommandLine` (2.0.0, MIT).
- **Forbidden Dependencies:** `Ornare.App`, `Ornare.DesignSystem`, WinUI/XAML types.
- **Packaged Execution Alias:** `uap5:AppExecutionAlias` pointing to `Ornare.Cli.exe` with alias `ornare.exe`.

---

## 2. Invariant & Architecture Checklist

| Rule / Decision | Requirement | Architecture Compliance Plan |
|---|---|---|
| `F24-DEC-01` | Separate Console Project | `Ornare.Cli` created as standalone executable with zero WinUI or design system references. |
| `F24-DEC-02` | System.CommandLine Pinned | Pinned to MIT release 2.0.0 in `Directory.Packages.props` and recorded in `THIRD-PARTY-NOTICES.md`. |
| `F24-DEC-03` | Pure Machine Output (`--json`) | In `--json` mode, stdout contains exclusively a single valid JSON envelope; all logging and diagnostics route to stderr. |
| `F24-DEC-04` | English-Only Human CLI | Human console output is English-only. JSON envelope is locale-neutral. |
| `F24-DEC-05` | Render Input Mutex | Exactly one of `--source <PATH>`, `--color <#HEX>`, `--preset <ID>` required for `apply`, `export-ico`, `export-png`. |
| `F24-DEC-06` | Render Modifier Matrix | `--template` valid only with `--color`; `--mode`, `--padding`, `--background` valid only with `--source`; `--recolor` valid only with SVG `--source`; `--preset` rejects all modifiers. Violations exit code 2. |
| `F24-DEC-07` | Fixed Command Grammar | Exactly 7 subcommands: `apply`, `apply-last`, `reset`, `undo`, `inspect`, `export-ico`, `export-png`. |
| `F24-DEC-08` | Versioned JSON Envelope | `{ "schemaVersion": 1, "command": "...", "success": bool, "code": "...", "messageKey": "...", "operationId": "...|null", "data": {...}|null }` serialized via source-generated `JsonSerializerContext`. |
| `F24-DEC-09` | Fixed Exit Codes | `0` (Success), `2` (InvalidArgs), `3` (UnsupportedTarget), `4` (Conflict), `5` (StorageError), `6` (Failed), `10` (Unexpected). |
| `F24-DEC-10` | AppExecutionAlias | Declared in `Package.appxmanifest` under `uap5:AppExecutionAlias` for `ornare.exe`. |

---

## 3. Test Strategy

1. **CLI Grammar & Validation Tests (`CliGrammarValidationTests`):**
   - Verify mutex: Missing selector, multiple selectors reject with exit code 2.
   - Verify invalid modifier combinations (e.g. `--template` with `--source`, `--mode` with `--color`, modifiers with `--preset`) reject with exit code 2.
   - Verify valid commands parse and execute accurately.
2. **Output Purity & JSON Schema Tests (`CliJsonContractTests`):**
   - Verify `--json` stdout purity: output parses as valid JSON with `schemaVersion = 1`.
   - Verify exit codes map correctly across success (0), argument error (2), unsupported target (3), external conflict (4), and missing history (4).
3. **Equivalence & Round-Trip Tests (`CliCoreEquivalenceTests`):**
   - Apply folder via CLI, verify `.ornare\folder.ico` and `desktop.ini` match GUI customization.
   - Inspect folder via CLI, verify metadata, icon path, and health status match SQLite.
   - Undo and Reset via CLI, verify baseline restoration and cleanup.
   - Export ICO and PNG via CLI, verify output files are created with correct frames and dimensions.
4. **Full Regression:**
   - Solution builds cleanly in Release (managed + native).
   - All tests pass (Core, Storage, Imaging, Windows, Integration, UiTests).
   - Managed ARM64 Release build clean (0 warnings, 0 errors).
   - Golden path verification clean.
