# ADR 0009: Native AOT Compatibility for Ornare Vertical Slice

## Status
Accepted

## Date
2026-09-24

## Context
Per Ornare Agent Contract and Gate 1 exit criteria, an empirical Native AOT experiment must be conducted on the actual dependency graph to evaluate whether Native AOT is viable without lowering security, relying on reflection hacks, or degrading maintainability.

The solution graph includes:
- `Ornare.App` (WinUI 3 / Windows App SDK 2.5.1 / .NET 10)
- `Ornare.Core` (Domain logic, journal state machine, source-generated JSON)
- `Ornare.DesignSystem` (WinUI 3 tokens and visual resources)
- `Ornare.Imaging` (Magick.NET 14.17.1 Q16 AnyCPU)
- `Ornare.Storage` (Microsoft.Data.Sqlite.Core + Windows system `winsqlite3.dll`)
- `Ornare.Windows` (CsWin32 P/Invoke generators, desktop.ini parser, shell refresh)

## Empirical Findings & Evaluation
1. **Source Generation**: Standard reflection-based `JsonSerializer` produced trimming warnings `IL2026` and `IL3050`. Introducing `[JsonSerializable(typeof(FolderIconSemantics))]` via `FolderJsonContext` completely eliminated reflection and trimming warnings.
2. **PRI Indexing**: Windows App SDK resource generation requires class library project references (`Ornare.DesignSystem`, `Ornare.Windows`) to build with the matching Runtime Identifier (`-r win-x64`) so that the project PRI indexer resolves referenced PRI files.
3. **Compilation**: `dotnet publish src\Ornare.App\Ornare.App.csproj -c Release -r win-x64 -p:PublishAot=true` succeeded with exit code 0 (`Generating native code`).
4. **PE Verification**:
   - Machine: x64
   - Size: ~10.6 MB (`Ornare.App.exe`)
   - Entry point: `wmainCRTStartup` (native CRT startup)
   - Security mitigations verified via `dumpbin /headers`: High Entropy ASLR, DEP (NX compatible), and CET compatible (`0x00000001 extended DLL characteristics: CET compatible`).
   - Zero CLR/IL header directories present.

## Decision
Native AOT is **VIABLE (PASS)** for the Ornare architecture.
- Core and storage layers are 100% trim- and AOT-safe with source generators.
- Native AOT will remain an active release profile alongside standard framework-dependent deployment.
- Maintainability is preserved: no reflection workarounds or security waivers were introduced.

## Consequences
- AOT build pipeline is verified and repeatable for x64.
- Source generators must continue to be used for any newly introduced serialized models.
