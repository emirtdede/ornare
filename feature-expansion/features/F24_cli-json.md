# F24 — CLI + Machine-Readable Output

**Status:** IMPLEMENT  
**Dependencies:** stable Core operations; F18; F22/F23  
**Normative parent:** `../00_SHARED_ARCHITECTURE_CONTRACT.md`

## Goal

Add a production Native-AOT-friendly CLI that invokes the same Core operations as the GUI and exposes deterministic arguments, JSON contracts and exit codes without initializing WinUI.

## Fixed decisions

1. Add separate `.NET 10` console project `Ornare.Cli` targeting the same Windows TFM/RIDs as the app. It references exactly `Ornare.Core`, `Ornare.Imaging`, `Ornare.Storage`, and `Ornare.Windows`; it must not reference `Ornare.App` or `Ornare.DesignSystem`.
2. Use `System.CommandLine`; implementation time must re-verify/pin a stable MIT version, Native AOT/trimming behavior and vulnerability status.
3. Global machine-output option is `--json`. In JSON mode **stdout contains exactly one JSON document and nothing else**; diagnostics go to stderr.
4. Human CLI output in this expansion is **English-only**. F27 does not load MRT/WinUI resources into CLI. JSON is locale-neutral.
5. A shared **Render Input** contract is used by `apply`, `export-ico`, and `export-png`: exactly one of:
   - `--source <PATH>`
   - `--color <#RRGGBB>`
   - `--preset <STABLE_PRESET_ID>`
   is required.
6. Render modifiers:
   - `--template <ornare-classic|ornare-soft|ornare-flat>` is valid only with `--color`; default `ornare-classic`.
   - `--mode <contain|cover|fill>`, `--padding <0..30>`, `--background <transparent|#RRGGBB>` are valid only with `--source`; defaults are `contain`, `0`, `transparent`.
   - `--recolor <#RRGGBB>` is valid only with an SVG `--source`; non-themeable SVG returns the F09 typed error.
   - `--preset` accepts no render modifiers; preset version/catalog defines its visual meaning.
7. Command grammar is exactly:
   - `ornare apply <FOLDER> (Render Input + valid modifiers)`
   - `ornare apply-last <FOLDER>`
   - `ornare reset <FOLDER>`
   - `ornare undo <FOLDER>`
   - `ornare inspect <FOLDER>`
   - `ornare export-ico (Render Input + valid modifiers) --output <PATH>`
   - `ornare export-png (Render Input + valid modifiers) --output <PATH> [--size <256|512|1024>]`
   F14 default PNG size is 1024.
8. F25 later adds `--dry-run` only to `apply`, `apply-last`, `reset`, `undo`, `export-ico`, `export-png`; `inspect` is already read-only and rejects `--dry-run` as invalid.
9. Stable JSON envelope:
   `{ "schemaVersion": 1, "command": "...", "success": true|false, "code": "...", "messageKey": "...", "operationId": "...|null", "data": {...}|null }`.
   Property names and `schemaVersion=1` are contract-stable for this release.
10. Exit codes are fixed:
   - `0` success
   - `2` invalid arguments
   - `3` unsupported target/input
   - `4` conflict/not-applicable
   - `5` access/storage error
   - `6` operation failed or recovery required
   - `10` unexpected internal error
11. MSIX exposes unique `ornare.exe` via `uap5:AppExecutionAlias` to `Ornare.Cli.exe`; alias package behavior is tested before feature certification.
12. CLI handlers construct typed Core requests only; there is no second business/mutation engine.

## Integration points

- `src/Ornare.Cli`: parser/handlers/console formatting.
- `Ornare.Core`: shared request/operation contracts.
- `Ornare.Imaging/Storage/Windows`: existing implementation services wired without App/WinUI.
- `Ornare.Package`: `uap5:AppExecutionAlias`.
- `System.Text.Json`: source-generated response serialization.

## Implementation sequence

1. Add `Ornare.Cli` project manually and wire existing DI/composition primitives without referencing WinUI assemblies.
2. Re-verify/pin `System.CommandLine` and record package/license/version/provenance.
3. Implement common Render Input binder/validator and the exact command grammar/option-validity matrix above.
4. Map handlers directly to shared Core commands; no raw `desktop.ini`, journal, SQLite or image mutation code in handler classes.
5. Implement the stable JSON envelope using source-generated `System.Text.Json` context; never reflection serialization.
6. Define stable domain-code → exit-code mapping and contract snapshot fixtures.
7. Human mode prints concise English result/error to stdout/stderr according to normal CLI conventions; machine mode keeps stdout JSON-pure.
8. Build `Ornare.Cli` as a console-subsystem executable (`OutputType=Exe`) and register exactly one packaged execution alias `ornare.exe` using `uap5:AppExecutionAlias` mapped to `Ornare.Cli.exe`; do not add registry/PATH fallbacks. Verify fresh install/update/uninstall/reinstall and alias-disable behavior.
9. Native AOT publish x64 and ARM64; verify CLI binary starts without WinUI/Manager initialization.
10. Benchmark cold/warm CLI startup independently.

## Risks and mandatory mitigations

| Risk | Mandatory mitigation |
|---|---|
| CLI/GUI semantic divergence | Shared Core requests/operations only. |
| Ambiguous command grammar | Fixed grammar and option-validity matrix above; invalid combinations exit 2. |
| AOT/reflection issues | Source-generated JSON; re-verified System.CommandLine AOT support; zero new AOT warnings. |
| Automation contract breaks | Versioned JSON schema + snapshot tests + stable exit codes. |
| Alias unavailable/disabled | Test package lifecycle; document that Windows can disable execution aliases; do not invent registry fallback. |
| WinUI startup overhead | CLI project cannot reference App/DesignSystem. |
| Sensitive output | JSON/human output uses existing redaction rules; never image bytes/secrets. |

## Required tests

- Parser tests for every command, required selector and every invalid modifier combination.
- `--json` stdout purity and JSON schema snapshot tests.
- Exit-code contract tests for each typed failure class.
- GUI vs CLI equivalence for apply/apply-last/reset/undo on identical fixtures.
- Export round-trip via CLI.
- AppExecutionAlias fresh install/update/uninstall/reinstall.
- x64 runtime smoke + x64/ARM64 Native AOT zero-warning builds.
- Verify CLI process does not load WinUI/Manager modules on normal commands.
- Cold/warm startup benchmark.

## Performance requirements

- No daemon/background process.
- No WinUI/App initialization.
- No duplicate render/database/mutation engine.
- Startup benchmark is tracked as its own critical metric under shared >10% regression protocol.

## Approved sources / research

- **System.CommandLine:** https://github.com/dotnet/command-line-api
- **uap5:AppExecutionAlias:** https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-uap5-appexecutionalias
- **Packaged .NET console alias guidance:** https://learn.microsoft.com/en-us/windows/apps/dev-tools/winapp-cli/guides/dotnet

Before copying any source, re-check exact upstream commit/license and record provenance according to `../01_OSS_SOURCES_AND_LICENSE_POLICY.md`.

## Forbidden implementations

- No hand-written ad-hoc parser.
- No `Ornare.App.exe --cli` WinUI bootstrap.
- No reflection-based JSON.
- No GPL ImageToIcon source reuse.
- No undocumented aliases/options accepted “for convenience”.

## Feature Definition of Done

- Exact grammar/JSON/exit-code contract tests green.
- Native AOT x64/ARM64 zero-warning builds.
- Alias/package lifecycle smoke green.
- CLI/GUI equivalence tests green.
- Full gate green.
- A feature evidence report exists under `production_artifacts/feature-gates/F24/` and records affected INV-* invariants, commands/tests, build hashes and performance delta.
- No temporary diagnostics, disabled tests, warning suppressions added solely to force green, or unrelated refactors remain.
