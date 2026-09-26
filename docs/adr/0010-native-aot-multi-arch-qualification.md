# ADR 0010: Multi-Architecture (x64 and ARM64) Native AOT Qualification

## Status
Accepted

## Date
2026-09-25

## Context
Following the Gate 2/3 expansion, an initial report generalized that "Windows App SDK / WinUI 3 does not directly support Native AOT" due to compilation errors encountered during ARM64 publish (`WMC1510`, `IL2026`, `IL3050`). 
Per Microsoft official documentation, Windows App SDK supports `PublishAot=true` via .NET 8/9/10 Native AOT and source generators.
A forensic investigation was conducted to determine the exact root causes of the publish failures and resolve them without compromising security or architectural invariants.

## Root Cause Analysis
A systematic diff between the earlier working x64 AOT vertical slice and the Gate 2/3 solution graph revealed five distinct issues:

1. **Reflection-Based Diagnostic Serialization (`IL2026`, `IL3050`)**:
   - Location: `ManagerWindow.xaml.cs:473`
   - Cause: `JsonSerializer.Serialize(report, s_jsonOptions)` called the reflection-based serializer requiring dynamic code generation and unreferenced code inspection.
2. **Incomplete Source-Generation Context**:
   - Location: `FolderJsonContext.cs`
   - Cause: Gate 2 domain entities (`DiagnosticReport`, `UserSettings`, `FolderAssignment`, `LibraryAsset`, `AssignmentHistoryEntry`, `FolderBaseline`, `JournalEntry`, etc.) were not registered in `FolderJsonContext`.
3. **XAML DataTemplate Trimming Warnings (`WMC1510`)**:
   - Location: `ManagerWindow.xaml` and `QuickPanelWindow.xaml`
   - Cause: DataTemplates used untyped `{Binding}` and an anonymous type in `QuickPanelWindow.xaml.cs:518` rather than strongly-typed, compiled-binding-compatible types with `x:DataType`.
4. **WinUI XamlTypeInfo Activator Collision (`CS9035`, `CS8852`)**:
   - Location: ViewModels marked with `[Microsoft.UI.Xaml.Data.Bindable]`
   - Cause: Properties used C# 11 `required` and `init;` accessors, conflicting with WinUI's code-generated activator in `XamlTypeInfo.g.cs` which instantiates types via parameterless constructor and property setters.
5. **Project PRI Output Path Alignment (`PRI252`)**:
   - Location: `Ornare.DesignSystem.csproj`
   - Cause: Missing `<Platforms>x64;ARM64</Platforms>` and `<RuntimeIdentifiers>win-x64;win-arm64</RuntimeIdentifiers>` caused `Ornare.DesignSystem.pri` to build to a non-RID directory, triggering a resource resolution failure during RID-specific WinAppSDK indexing.

## Applied Corrections (Zero-Hack Compliance)
In strict accordance with Ornare non-negotiable rules:
- No unsafe blanket trim roots or indiscriminate assembly trimming suppressions were added.
- `FolderJsonContext` was expanded with `[JsonSerializable]` attributes for all domain, diagnostic, and journal models with indented source generation.
- `ManagerWindow.xaml.cs` was updated to use `FolderJsonContext.Default.DiagnosticReport`.
- ViewModels (`LibraryItemVm`, `RecentItemVm`, `FolderItemVm`, `RecentStripItemVm`) were formally defined, marked with `[Microsoft.UI.Xaml.Data.Bindable]`, and given standard `{ get; set; }` properties.
- DataTemplates in `ManagerWindow.xaml` and `QuickPanelWindow.xaml` were decorated with explicit `x:DataType="local:..."`.
- `Ornare.DesignSystem.csproj` was aligned with `Platforms` and `RuntimeIdentifiers`.

## Measured Results & Verification

### 1. x64 Native AOT Publish
- Command: `dotnet publish src/Ornare.App/Ornare.App.csproj -c Release -r win-x64 -p:PublishAot=true -p:Platform=x64`
- Result: **0 Errors, 0 Warnings** (Exit code 0)
- Binary: `Ornare.App.exe` (~12.6 MB)
- Entry Point: `wmainCRTStartup`
- Security Mitigations: DEP (NX), High Entropy ASLR, Dynamic Base, CFG.

### 2. ARM64 Native AOT Publish
- Command: `dotnet publish src/Ornare.App/Ornare.App.csproj -c Release -r win-arm64 -p:PublishAot=true -p:Platform=ARM64`
- Result: **0 Errors, 0 Warnings** (Exit code 0)
- Binary: `Ornare.App.exe` (~12.5 MB)
- Machine Type (`dumpbin /headers`): `AA64 machine (ARM64)`
- Entry Point: `wmainCRTStartup`
- Dependencies: `Magick.Native-Q16-arm64.dll` (verified AA64), `DirectML.dll` (AA64), `onnxruntime.dll` (AA64).
- Security Mitigations: DEP (NX), High Entropy ASLR, Dynamic Base, Terminal Server Aware, CFG.

## Decision
Native AOT is **VERIFIED and QUALIFIED** as viable and production-ready for **both x64 and ARM64**.
- Windows App SDK / WinUI 3 Native AOT compatibility is fully operational when source generators and strongly-typed XAML bindings are properly configured.
- Ornare V1 adopts Native AOT for both x64 and ARM64 production packages.

## Consequences
- Eliminates CoreCLR runtime startup penalty on both x64 and ARM64.
- Reduces memory footprint and attack surface by removing JIT compilation pages at runtime.
- Any future serialized model or XAML-bound entity must be explicitly declared in source-generation contexts and attributed with `[Bindable]` with explicit `x:DataType`.
