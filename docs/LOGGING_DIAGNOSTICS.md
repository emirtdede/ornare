# Logging and Diagnostics Policy

Ornare V1 has no telemetry/analytics backend. Diagnostics are local-first and privacy-minimized.

## Runtime logging

Default production behavior:

- no continuous upload;
- no verbose persistent log containing folder paths;
- maintain a small in-memory diagnostic ring buffer of structured event codes, timings, HRESULT/error categories and correlation IDs;
- data is lost on normal exit unless the user explicitly exports diagnostics or enables a temporary diagnostic session.

## Diagnostic export

User-triggered export may contain:

- Ornare/version/package identity;
- Windows version/build and architecture;
- anonymized/redacted target categories;
- operation/error codes;
- timing counters;
- dependency/runtime version inventory;
- UIA/accessibility configuration relevant to a bug;
- sanitized recent diagnostic events.

It must not contain:

- image bytes/thumbnails;
- clipboard contents;
- secrets/tokens;
- full folder paths by default;
- arbitrary desktop.ini contents unrelated to the issue.

If the user explicitly opts to include paths for support, clearly disclose that before export.

## Crash handling

- Do not attempt complex recovery logic inside an unhandled-exception handler.
- Persist only minimal crash/reconciliation markers using already-safe mechanisms.
- On next launch, inspect unfinished operation journal and reconcile.
- Windows Error Reporting/Store crash facilities may be used according to platform policy; Ornare does not add a custom telemetry backend in V1.
