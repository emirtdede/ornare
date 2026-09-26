# Ornare Package and Payload Audit (GAP_CLOSURE_F19_F24)

## Summary
- **MSIX Candidate (x64)**: `production_artifacts/release/candidate/Ornare_1.0.0.0_x64.msix`
  - SHA256: `249D348741C4E87C51A2997B794F356514330A027D3225FCEE0DB6FE1EE03362`
  - Signature: Valid (`CN=Vellium`, SHA256 digest)
- **MSIX Candidate (ARM64)**: `production_artifacts/release/candidate/Ornare_1.0.0.0_arm64.msix`
  - SHA256: `06A20FA7EEA3D32106DBDCEFE65D453529FCC0CF97EC54B2AA424E627B1F2711`
  - Signature: Valid (`CN=Vellium`, SHA256 digest)
- **MSIX Bundle**: `production_artifacts/release/candidate/Ornare_1.0.0.0_x64_arm64.msixbundle`
  - SHA256: `AA9864B920B1B41C6CD11CBF221762B8BB4C1C2E7280B05762B138E96D7DF735`
- **Installer**: `production_artifacts/release/candidate/OrnareSetup.exe`
  - SHA256: `5992C58F92C25159C4EEB9E9599EB8C508244E579325F88564150AF032F6D100`

## Executable Payload Verification
| Executable / Library | Machine Type | Subsystem | Purpose |
| :--- | :--- | :--- | :--- |
| `Ornare.App.exe` | AMD64 (x64) | Windows GUI | Native AOT WinUI 3 main app (Quick Panel / Manager) |
| `Ornare.Cli.exe` | AMD64 (x64) | Windows CUI | Native AOT CLI (`ornare.exe` alias) |
| `Ornare.ShellExtension.dll` | AMD64 (x64) | DLL (STA) | Native C++ Windows 11 Explorer Command Provider |
| `Magick.Native-Q16-x64.dll` | AMD64 (x64) | DLL | Image processing native backend |
| `DirectML.dll` / `onnxruntime.dll` | AMD64 (x64) | DLL | AI / Machine Learning runtimes |
| `Microsoft.WindowsAppRuntime.Bootstrap.dll` | AMD64 (x64) | DLL | Windows App SDK Dll |
| `WebView2Loader.dll` | AMD64 (x64) | DLL | WebView2 Runtime loader |

## Manifest & Execution Alias Audit
- Namespace: `xmlns:uap5="http://schemas.microsoft.com/appx/manifest/uap/windows10/5"`
- Namespace: `xmlns:desktop4="http://schemas.microsoft.com/appx/manifest/desktop/windows10/4"`
- Application: `Id="OrnareCli" Executable="Ornare.Cli.exe" EntryPoint="Windows.FullTrustApplication" desktop4:SupportsMultipleInstances="true"`
- Extension: `Category="windows.appExecutionAlias"`
- AppExecutionAlias: `<uap5:AppExecutionAlias desktop4:Subsystem="console"><uap5:ExecutionAlias Alias="ornare.exe" /></uap5:AppExecutionAlias>`
- Conforms to Windows AppX Schema specification.

## Hygiene Checklist
- [x] Zero `.pdb` files present in layout or package.
- [x] Zero `.cs`, `.cpp`, `.h`, `.vcxproj`, or `.csproj` source files present.
- [x] Zero scratch or temporary test files.
- [x] Zero test assemblies or benchmark artifacts.
- [x] All native binaries match expected target architecture (x64 in x64 package, ARM64 in ARM64 package).
- [x] PRI resources compiled and validated with makepri (`resources.pri`).
