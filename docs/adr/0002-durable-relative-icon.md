# ADR 0002 - Folder-owned relative ICO plus canonical library

Status: Accepted

## Decision

Keep a content-addressed canonical asset in app-local storage for reuse/recovery, and place an Ornare-owned hidden ICO inside each customized folder referenced relatively from `desktop.ini`.

## Consequences

Improves survival after source deletion, app uninstall and ordinary local moves; uses small per-folder disk duplication by design. Requires careful provenance/cleanup and `desktop.ini` preservation.
