# Error and Edge-Case Matrix

| Scenario | Expected behavior |
|---|---|
| User drops non-image file | Keep panel open; localized `Choose an image file.` No partial writes. |
| Corrupt supported extension | `This image can't be read.` Details may show technical code. |
| Very large/decompression bomb | Reject under resource policy; no crash/hang. |
| SVG references remote resource | Do not fetch; reject or render with external resource disabled. |
| Clipboard contains URL only | Do not download; ask user to copy image itself or save locally. |
| Target folder deleted before apply | Abort, explain folder no longer exists. |
| Folder loses actual write permission / ACL denies writes | Abort, preserve prior state. |
| Protected system folder | Refuse safely; no UAC prompt. |
| Existing desktop.ini has unrelated keys | Preserve them. |
| Existing third-party custom icon before first/new apply | Capture current icon semantics as baseline. Reset restores them only while Ornare ownership is uncontested. |
| Icon changed externally after Ornare apply | Mark `ExternalIconConflict`; no automatic Repair/Reset write. Explicit new Ornare apply captures the external state as fresh baseline; Stop tracking leaves live state untouched. |
| Disk full during ICO write | Roll back; prior icon survives. |
| Failure after ICO but before desktop.ini commit | Remove safe temp/orphan Ornare file; prior state survives. |
| Failure after desktop.ini replace before DB commit | Reconcile/rollback using transaction journal; do not pretend success. |
| Explorer doesn't repaint immediately | Retry only bounded targeted supported refresh; if durable disk state is correct, surface `SucceededWithRefreshWarning` rather than undoing correct state. Never kill Explorer automatically. |
| User drops second image while first processing | Before commit, deliberate replacement may cancel preparation; after commit begins, queue/coalesce one pending activation/input and never interleave folder writes. |
| App already open | Redirect activation to existing instance. Switch context only when safe; during commit queue/coalesce one pending target according to `STATE_MACHINES.md`. |
| Multiple folders selected | Command hidden/disabled in V1. |
| Network/UNC/WebDAV path | Unsupported/caveat according to Windows trust behavior; no security weakening. |
| OneDrive placeholder not hydrated | Avoid forced broad hydration; explain if input/target isn't locally available. |
| Original source deleted after success | No effect on applied icon. |
| Folder renamed/moved locally | Relative icon remains; DB reconciles path/file identity when manager observes it. |
| App uninstalled keep customizations | Folder icon remains. |
| High Contrast | Material/visual effects degrade to accessible system resources. |
| Reduce transparency | UI remains clear without glass. |
| Turkish path/non-ASCII | Full Unicode behavior; no mojibake or shell failure. |
| Long path | Respect `SHGetSetFolderCustomSettings` path limits. Use a direct Unicode `desktop.ini` fallback only after `LONG_PATH_COMPATIBILITY.md` qualification; otherwise reject before mutation. Never truncate/guess. |
