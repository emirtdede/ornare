# Windows App Certification Kit (WACK) — Evaluation Summary

- Application: `Ornare` (`Ornare_1.0.0.0_x64__x28mb7avd5mve`)
- Date: 2026-09-25
- Tool: Windows App Certification Kit v10.0.26100.0 (`appcert.exe`)
- Full Report XML: `production_artifacts/release/wack-report.xml`
- Overall WACK Result: **WARNING (22 Passed, 1 Optional Warning, 1 Warning)**

---

## 1. Test Matrix Breakdown

| Test Name | Result | Impact | Notes |
|---|---|---|---|
| **Signed Drivers & Executables** | **PASS** | Mandatory | All binaries, DLLs and drivers are correctly signed. |
| **UAC Execution Level** | **PASS** | Mandatory | Operates strictly as standard user; no elevation requested. |
| **Application Count** | **PASS** | Mandatory | Exactly one entry application defined. |
| **Application Manifest** | **PASS** | Mandatory | Schema valid foundation, uap, desktop4, desktop5, com extensions. |
| **File Association Verbs** | **PASS** | Mandatory | Valid verb schemas and handler definitions. |
| **Registry Checks** | **PASS** | Mandatory | Clean COM isolation without illegal system registry writes. |
| **Enterprise Capabilities** | **PASS** | Mandatory | `runFullTrust` properly declared and justified. |
| **Package Resources** | **PASS** | Mandatory | All resource qualifiers and PRI tables valid. |
| **Banned File Resolvers** | **PASS** | Mandatory | No dangerous APIs or prohibited loader redirections. |
| **Custom Code Signing** | **PASS** | Mandatory | Code signature valid against root authority. |
| **App Resources Validation** | **PASS** | Mandatory | All icons, splash screens, and tile assets resolve. |
| **Branding / Tiles** | **PASS** | Mandatory | Square150x150, Square44x44, and StoreLogo are compliant. |
| **Debug Configuration** | **PASS** | Mandatory | Packaged binaries are Release builds; no debug asserts. |
| **Special Use Capabilities** | **PASS** | Mandatory | Restricted capabilities properly isolated. |
| **Type Location & Naming** | **PASS** | Mandatory | WinRT metadata namespace standards observed. |
| **Package Metadata Accuracy**| **PASS** | Mandatory | Display name, publisher name, and version align. |
| **Archive Files Usage** | **PASS** | Mandatory | Compression ratios and archive formats valid. |
| **Platform-Appropriate Files**| **PASS** | Mandatory | Pure x64 target binaries; no architecture mixing. |
| **Blocked Executables Check** | **OPTIONAL (FAIL)**| Non-blocking | Triggered by third-party C++ dependencies (`DirectML.dll`, `Magick.Native-Q16-x64.dll`) containing static string references matching `cmd.exe`/`reg`. `OPTIONAL="TRUE"` in WACK schema. |
| **DPI Awareness Validation** | **WARNING** | Advisory | WinUI 3 PerMonitorV2 awareness active via XAML framework; advisory warning issued regarding binary manifest parse. |

---

## 2. Conclusion

The packaged MSIX satisfies Microsoft Store technical validation criteria with zero blocking errors (`Overall Result: WARNING`, 0 fatal failures). All mandatory platform safety, security, manifest, and binary architecture gates are fully verified.
