# Long Path Compatibility Qualification

Windows folder customization has an important API constraint: the documented `SHGetSetFolderCustomSettings` folder-path parameter is bounded by `MAX_PATH`. Ornare must not truncate, normalize into the wrong target, or silently claim success for longer paths.

## Policy

1. Detect the path/API capability before mutation.
2. Prefer documented shell customization where it safely supports the target.
3. A direct Unicode `desktop.ini` path may be used only if the implementation has passed this qualification suite and preserves all invariants.
4. If no proven-safe path exists, fail before mutation with a localized unsupported-path message.

## Qualification suite

Test on supported Windows builds with paths:

- just below the relevant boundary;
- exactly at boundary-like lengths;
- above `MAX_PATH` with long-path-capable filesystem access;
- Unicode/non-Latin names;
- trailing-dot/space-invalid cases rejected by normal Windows semantics;
- rename from short to long and long to short;
- apply/reset/undo/repair;
- unrelated `desktop.ini` fields;
- folder move;
- app restart and reconciliation.

Pass requires Explorer to render the intended icon and Reset to preserve unrelated live configuration. Any ambiguous shell behavior means the target remains unsupported.
