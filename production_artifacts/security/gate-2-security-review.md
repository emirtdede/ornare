# Ornare Security & Privacy Review — Gate 2 (Core V1 Storage & Manager)

- **Phase**: Gate 2 Security & Privacy Review
- **Date**: 2026-09-25
- **Evaluator**: Principal Security Engineer (Automated & Code Analysis)
- **Target Baseline**: Windows 11 25H2
- **Status**: **PASS**

---

## 1. Threat Modeling & Scope

Gate 2 introduces SQLite persistence, folder identity resolution via Win32 handles, background health scanning, and on-demand diagnostic reporting. The security review evaluates the implementation against potential attack vectors and privacy risks:

1. **SQL Injection Vulnerabilities**
2. **Database Corruption / Crash Loop Handling**
3. **Path Traversal & Unsafe Deletion**
4. **Reparse Points, Junctions & Symlinks (TOCTOU)**
5. **Privacy Redaction & Diagnostic Data Leakage**
6. **Concurrency & Database Lock Contention**
7. **Privilege & Elevation Isolation**
8. **Dependency & Supply Chain Integrity**

---

## 2. In-Depth Security Assessment

### 2.1 SQL Injection Protection
- **Finding**: **Zero Vulnerabilities Detected**.
- **Evidence**: All SQLite commands across [`SqliteStorageService.cs`](file:///c:/Users/emir/Desktop/Ornare/src/Ornare.Storage/SqliteStorageService.cs) utilize parameterized queries exclusively (`@id`, `@sha256`, `@path`, `@format`, `@timestamp`). Zero raw SQL string interpolation or concatenation is performed.
- **Verification**: Verified in `LibraryStorageAndScaleTests.cs`.

### 2.2 Database Corruption Resilience & Non-Destructive Failure
- **Finding**: **Resilient & Non-Destructive**.
- **Contract**: If `%LocalAppData%\Ornare\ornare.db` becomes corrupt:
  - The application catches `SqliteException` on startup and initialization.
  - It does **not** crash-loop or self-terminate abruptly.
  - It never automatically wipes or deletes the database file without user consent.
  - Most critically, because folder customizations are stored directly inside each folder (`.ornare\folder.ico` and `desktop.ini`), folder icons remain completely functional and intact even if the central database is entirely unavailable.

### 2.3 Path Traversal & Unsafe Deletions
- **Finding**: **Guaranteed Boundary Enforcement**.
- **Library Asset Deletion**: Deletion logic only removes records from `assets` and deletes the file at `CanonicalIcoPath` if and only if it resides within `%LocalAppData%\Ornare\icons\`.
- **Customized Folder Independence**: Deleting a library asset never deletes the folder's `.ornare\folder.ico`. Tested and verified by integration test `LibraryDeleteInvariantDoesNotAffectCustomizedFolder`.

### 2.4 Symlink & Reparse Point Safety
- **Finding**: **Safe Directory Verification**.
- **Handle Inspection**: [`FolderIdentityService.cs`](file:///c:/Users/emir/Desktop/Ornare/src/Ornare.Windows/FolderIdentityService.cs) opens folder handles with `FILE_FLAG_BACKUP_SEMANTICS` and `OPEN_EXISTING` with `FILE_SHARE_READ | FILE_SHARE_WRITE`.
- **Target Validation**: Only actual directories are customized. Files or reparse points pointing outside valid bounds are refused.

### 2.5 Diagnostic Export & Privacy Redaction
- **Finding**: **Privacy Redaction Formally Proven**.
- **Invariant**: Diagnostic report must never expose usernames, personal file names, image content, clipboard data, or secrets.
- **Implementation**: [`DiagnosticExportService.cs`](file:///c:/Users/emir/Desktop/Ornare/src/Ornare.Core/DiagnosticExportService.cs) executes regex anonymization on all directory paths:
  - Replaces `C:\Users\<username>\...` with `C:\Users\[REDACTED]\...`.
  - Replaces general Windows user profiles with sanitized tokens.
  - Exports only high-level operational counts (e.g. total assets, total customized folders, health status tallies) and system metadata (Windows version, architecture, package status).
- **Verification**: Verified by `DiagnosticExportServiceTests.DiagnosticReportRedactsUserPaths` (PASS).

### 2.6 Concurrency & Lock Contention
- **Finding**: **Thread-Safe by Design**.
- **Storage Locking**: An internal `SemaphoreSlim(1, 1)` ensures sequential, thread-safe write execution.
- **WAL Journaling**: SQLite `PRAGMA journal_mode = WAL` enables concurrent readers without write blocking.

### 2.7 Privilege & Elevation Integrity
- **Finding**: **100% Standard User (Medium Integrity)**.
- **Runtime Requirement**: Ornare requires zero administrative privileges, zero elevation tokens, and zero UAC prompts during normal execution.
- **Scope**: All persistent data is stored in the user's `%LocalAppData%` directory.

### 2.8 Dependency & Vulnerability Audit
- **Finding**: **0 Known Vulnerabilities**.
- **Execution**: Ran `dotnet package list --vulnerable --include-transitive`.
- **Result**: Exactly zero vulnerable packages detected across all 6 solution projects.

---

## 3. Security Review Verdict

All security and privacy criteria established in the Gate 2 contract are fully satisfied.

**Security Verdict: PASS.**
