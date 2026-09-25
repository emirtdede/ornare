# Security and Privacy Specification

## 1. Threat model summary

Primary risks:

- malformed/untrusted image inputs;
- native codec vulnerabilities;
- SVG external references;
- resource exhaustion/decompression bombs;
- shell extension destabilizing Explorer;
- unsafe writes to user folders / `desktop.ini` corruption;
- path traversal / reparse-point surprises;
- race conditions during folder modification;
- malicious/changed folder between shell invocation and app operation;
- package/update/signing compromise;
- overbroad file permissions;
- accidental exfiltration in telemetry/diagnostics.

## 2. Privilege

V1 runs as standard user. Do not request elevation to customize protected folders. Refuse safely instead.

## 3. Image trust boundary

Treat every input image as untrusted. Enforce `IMAGE_PIPELINE.md` policy before full decode/render. Keep Magick.NET and native dependencies current and audit vulnerabilities on every release.

## 4. File operations

- canonicalize paths;
- validate target remains a directory;
- reject target directories that are reparse points/symlinks/junctions in V1 and revalidate identity before mutation;
- avoid following unexpected links during cleanup;
- use least-privilege file access;
- do not overwrite unrelated user files;
- Ornare-owned files have unmistakable provenance/naming.

## 5. Shell extension

Explorer process is a high-availability boundary. Native extension must be tiny and hardened. Never load image codecs/database/UI into Explorer.

## 6. `desktop.ini`

Merge/preserve user state. Capture only the typed pre-Ornare icon semantics and attribute ownership needed for semantic restore; never depend on overwriting the current file with a historical full-file backup. Do not use global security weakening to force remote folder customization.

## 7. Privacy

No telemetry or analytics SDK in V1. No image upload. No user account. No remote endpoint required by core functionality.

Diagnostics export is user-initiated and redacts folder paths by default. Never include image content.

## 8. Secrets

No secret belongs in repo, XAML, scripts, package manifest, MCP config, or CI logs. Signing credentials use secure CI/store facilities.

## 9. Dependency security

CI/release process must:

- restore from locked/pinned packages;
- run NuGet vulnerability audit;
- review transitive native components;
- generate license inventory;
- block every unresolved Critical/High known vulnerability in shipped dependencies. Moderate exceptions require documented exploitability analysis, owner and time-bounded remediation under `DEPENDENCY_SUPPLY_CHAIN.md`.

## 10. Update trust

Primary Store distribution uses Store signing/update channel. Secondary package distribution must use a trusted code-signing solution; self-signed certificates are development-only.
