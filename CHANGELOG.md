# Changelog

All notable changes to Ornare will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.0-rc.1] - 2026-09-25

### Added
- **Windows 11 Modern Context Menu Integration**: Native C++ Win32 `IExplorerCommand` shell extension (`Ornare.ShellExtension.dll`) registering "Set Folder Icon" / "Klasör Simgesini Ayarla" on directories in Windows 11 File Explorer.
- **Quick Panel Surface**: Minimalist, card-free WinUI 3 quick panel supporting drag-and-drop, file picker, clipboard paste, Recent icons strip, Favorites strip, and instantaneous Undo / Reset actions.
- **High-Fidelity Imaging Pipeline**: Secure Magick.NET-based image decoder generating true multi-resolution 10-frame Windows `.ico` files (16, 20, 24, 32, 40, 48, 64, 96, 128, 256 px) directly from normalized source pixels with Lanczos filtering.
- **Durable Persistence Engine**: Folder-owned customization architecture storing icons locally in `.ornare\folder.ico` with relative path references, ensuring folder mobility across paths and volumes without broken links.
- **Desktop.ini Semantic Merge**: Safe merger preserving third-party sections (`[ViewState]`, `InfoTip`, localized resource names, comments) during apply and reset operations.
- **Transactional Journal & Crash Recovery**: SQLite-backed write-ahead transaction journal with automatic, idempotent startup reconciliation for interrupted operations.
- **Conflict & Health Detection**: Active scanning identifying orphaned icons, configuration mismatches, missing folder attributes, and external customizations without destructive overwrites.
- **Manager Application**: Unified desktop interface featuring Library grid with real-time search, Recent history, Favorites management, Customized Folders list with one-click repair, Settings with live theme and language toggle, and Privacy-safe Diagnostic Export.
- **First-Class Bilingual Localization**: Complete parity for English (`en-US`) and Turkish (`tr-TR`) across all UI surfaces, strings, and shell verbs.
- **Comprehensive Accessibility**: Full keyboard navigation, logical tab ordering, High Contrast theme adaptation, and UI Automation accessible names on all interactive controls.
- **MSIX Packaging**: Self-contained x64 MSIX package targeting Windows 11 (build 26100+), compliant with Windows App Certification Kit (WACK) standards.

### Security & Privacy
- **100% Offline & Local**: Zero network capabilities declared in AppxManifest, zero network API dependencies, zero outbound telemetry, zero analytics, zero external logging.
- **Standard User Execution**: Requires no administrative elevation, no UAC prompts, and no dangerous privilege requirements.
- **Strict Image Security Policy**: Explicit format allowlist (PNG, JPEG, WebP, BMP, GIF, TIFF, ICO), memory/disk resource caps (256 MB memory / 512 MB disk), decompression bomb rejection, and exclusion of untrusted SVG script execution.
- **Path Anonymization**: Diagnostic export automatically redacts user identifying profile paths in diagnostic logs.

### Known Limitations
- **ARM64 Physical Execution**: Managed code compiles cleanly for ARM64; however, physical ARM64 hardware qualification is NOT PROVEN in this test environment.
- **Physical Reboot Persistence**: Post-reboot folder icon caching is logically guaranteed by Windows Explorer relative `.ini` architecture, but physical OS reboot validation remains NOT PROVEN within the continuous agent environment.
- **Drive Support**: Fully verified on local NTFS volumes; cloud-only placeholder folders and remote network/WebDAV shares are unsupported in V1.
- **Commercial Release Gate**: Signed with development certificate (`CN=Ornare`); commercial Microsoft Store distribution requires production trusted signing and trademark clearance.

---

## [0.1.0] - 2026-09-24

### Initial Baseline
- Established architecture invariants, threat model, specification baseline, and initial engineering structure.
