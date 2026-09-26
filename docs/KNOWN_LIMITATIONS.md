# Ornare V1 — Known Limitations and System Boundaries

This document outlines the architectural and operational boundaries of Ornare V1 Release Candidate. Ornare adheres to strict truth-in-engineering principles: limitations are documented explicitly rather than masked.

---

## 1. Filesystem & Drive Boundaries

### 1.1 Local NTFS Drives
- **Supported**: Standard local NTFS partitions (fixed drives and local physical volumes) are fully supported.
- **Behavior**: All folder attributes (`+r`), desktop.ini (`+h+s`), and `.ornare\folder.ico` (`+h+s`) persist accurately.

### 1.2 Remote Network Drives / UNC Shares / WebDAV
- **Unsupported**: Network drives (e.g. `\\server\share`, SMB, NFS, WebDAV, mapped drive letters pointing to UNC shares) are rejected with `ErrorCode.InvalidTarget` or `ErrorCode.AccessDenied`.
- **Rationale**: Windows Explorer folder customization attributes across network filesystems exhibit nondeterministic caching and lack atomic relative journal guarantees.

### 1.3 Cloud Synchronized Folders (OneDrive, Dropbox, Google Drive)
- **Local Cache Supported**: Folders pinned locally ("Always keep on this device") function normally.
- **Caveat**: Cloud-only placeholder targets (files/directories with reparse point attribute or dehydrated state) are rejected to prevent unwanted cloud hydration or sync contention.

### 1.4 Protected System Directories
- **Prohibited**: Windows system directories (`C:\Windows`, `C:\Program Files`, `C:\Program Files (x86)`, `C:\Users\Default`, etc.) are actively blocked by `TargetFolderValidator` to prevent system corruption.

---

## 2. Platform & Hardware Verification

### 2.1 x64 Architecture
- **Status**: **VERIFIED / SUPPORTED FOR V1 INITIAL RELEASE**
- Fully validated on Windows 11 Version 24H2 / 25H2 (Build 26100+) x64 across native Shell Extension, WinUI 3 UI surfaces, imaging engine, persistent storage, and WACK certification.
- Supported architecture for Ornare V1 initial public distribution: **Windows 11 x64**.

### 2.2 ARM64 Architecture
- **Status**: **BUILD READY / PENDING PHYSICAL HARDWARE CERTIFICATION**
- Implementation status: Toolchain (MSVC ARM64), native C++ ShellExtension, managed graph (.NET 10), Native AOT publish, and MSIX packaging are 100% verified with 0 errors.
- Scope Decision: Per Product Owner release scope decision, ARM64 public release is deferred pending physical runtime verification on genuine Windows 11 ARM64 hardware (e.g., Qualcomm Snapdragon X Elite / Copilot+ PC) using the 19-point validation protocol. Scheduled for a subsequent certified release.

### 2.3 Physical Reboot Persistence
- **Status**: **VERIFIED**
- Formally verified on Windows 11: After physical OS reboot, folder ReadOnly attributes, desktop.ini (+H+S), and .ornare\folder.ico (+H+S) persist identically with matching SHA-256 hashes and passive Explorer icon rendering without Ornare processes running.

---

## 3. Image Formats

### 3.1 Supported Input Formats
- PNG, JPEG/JPG, WebP, BMP, GIF (first frame), TIFF (first page), ICO.

### 3.2 Unsupported / Deferred Formats
- **SVG**: Vector SVG rasterization is deliberately disabled in the default image security policy to prevent XML External Entity (XXE), Server-Side Request Forgery (SSRF), and script injection vulnerabilities.
- **AVIF / HEIC / HEIF**: Excluded from V1 baseline due to missing native platform codecs across all target Windows 11 configurations.

---

## 4. Operational Boundaries

### 4.1 Single-Target Operation
- Ornare V1 is designed strictly for single-folder curation. Batch folder customization, recursive child-folder processing, and bulk rule-based icon application are intentionally excluded from V1 scope.

### 4.2 Local-Only Operation
- Ornare operates completely offline without user accounts, cloud synchronization, or external web search.
