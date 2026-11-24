# Ornare - Threat Model

This is a living model of what can go wrong and how Ornare refuses to make it worse. It is
reviewed whenever a trust boundary changes (`docs/SECURE_SDLC.md`).

## Assets

| Asset | Why it matters |
|-------|----------------|
| User folders and their contents | Must never be deleted, renamed or moved by Ornare. |
| `desktop.ini` | Shared with Explorer and other tools; corrupting it breaks the folder view. |
| Ornare SQLite store | Holds user intent (assignments, library, settings, journal). |
| Imported images and generated ICOs | Untrusted input; a decoder defect could be exploited. |
| Diagnostic report | Leaving the device only when the user chooses; must not leak paths. |

## Trust boundaries

1. **Untrusted image bytes -> decoder.** A user can select any file.
2. **Explorer activation arguments -> process.** Another process can launch Ornare with paths
   and query parameters.
3. **Shared cloud folder -> snapshot reader.** The folder may contain attacker-modified JSON.
4. **Another process -> `desktop.ini`.** An external tool can rewrite the file between reads.

## Threats and mitigations

| Threat | Boundary | Mitigation |
|--------|----------|------------|
| Malicious image crashes or exploits the decoder | 1 | Size and frame-count limits, SVG security validation, malformed corpus tests. |
| Crafted SVG executes script or fetches remote resources | 1 | `SvgSecurityValidator` rejects scripting, external references and unsafe constructs before decode. |
| Overwriting another tool's icon | 3, 4 | Ownership check; `ExternalConflict` returned unless the user forces resolution (INV-FS-005). |
| Partial `desktop.ini` after a crash | 3, 4 | Atomic temp-file write with write-through rename (INV-FS-002). |
| Losing user data on crash mid-apply | 2, 3 | Durable journal plus idempotent reconciliation (INV-TX-001, INV-TX-002). |
| Two operations corrupt one folder | 2 | Per-target lock with timeout (INV-CON-001). |
| Path traversal or UNC escape | 2 | `TargetFolderValidator` rejects invalid formats and UNC targets in V1. |
| SQL injection through identifiers | 2, 3 | Parameterized SQL; interpolated identifiers are compile-time constants, guarded. |
| Diagnostic report leaks user identity | 3 | Path redaction before retention; report is local until the user shares it. |
| Tampered cloud snapshot injects data | 3 | Snapshot is validated on read; restore is user-confirmed; no code or path is executed from it. |
| Elevation or background persistence | 2 | No hidden admin requirement; no service, driver or startup task; the tray icon dies with the process. |

## Out of scope for V1

- Network shares (UNC) as customization targets.
- Multi-user concurrent access to one Ornare store.
- Cloud providers' own sync conflicts beyond the retained previous snapshot.

## Review triggers

Update this file when adding: a new file format, a new native dependency, a new persistence
store, a new activation channel, or a new way the product reads data it did not write.
