# Production Evidence

This directory is for generated **evidence**, not production source and not generated UI/code.

Expected milestone structure:

- `bootstrap/` — environment/version/toolchain reports;
- `architecture/` — vertical-slice and architecture review reports;
- `security/` — threat-model deltas, audits, fault-injection evidence;
- `design/` — Light/Dark/High Contrast screenshots and UI rubric reports;
- `performance/` — benchmark machine manifest, P50/P95 captures, WPR/WPA references;
- `quality/` — gate reports and traceability evidence;
- `release/` — WACK, SBOM, package hashes/signatures, install/update/uninstall evidence.

Large raw traces/binaries may be stored in approved CI artifact storage instead of Git; commit only durable summaries/references when appropriate. Never commit secrets or private user content.
