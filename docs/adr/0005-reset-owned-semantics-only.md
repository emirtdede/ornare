# ADR 0005 - Reset restores owned icon semantics, not a full desktop.ini snapshot

Status: Accepted

## Decision

Persist the pre-Ornare icon-related semantics/attribute ownership required for restore. Reset re-reads the live `desktop.ini` and changes only Ornare-owned icon semantics.

## Rationale

A whole-file snapshot restored later could erase unrelated settings added or changed by the user/Windows/another application after Ornare's first customization. Semantic merge is safer and respects external state.

## Consequences

- baseline model stores icon keys/presence/values and relevant attribute ownership;
- external icon changes are treated as conflicts rather than silently overwritten;
- parser/merge and failure injection tests are release-critical.
