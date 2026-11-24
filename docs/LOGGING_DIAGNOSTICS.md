# Ornare - Logging and Diagnostics

Ornare is local-first and privacy-minimized. Logging exists to diagnose the user's own
installation, never to collect analytics. There is no telemetry, no network transport and no
account.

## Principles

1. **Local only.** Nothing leaves the device. The diagnostic report is a file the user can
   choose to share; Ornare never uploads it.
2. **Redacted by default.** Absolute paths are reduced before retention so an accidental log
   share does not disclose the user's name or folder layout.
3. **Off by default.** Verbose persistent logging requires an explicit, temporary diagnostic
   session.
4. **Bounded.** The in-memory buffer and any log file are size-capped.
5. **Never on the UI thread.** File writes happen on a background writer.

## In-memory ring buffer

- Implemented in `src/Ornare.App/DiagnosticLog.cs`.
- Holds the most recent 256 entries.
- Every entry is redacted before it is stored.

## Temporary diagnostic session

- Enabled by setting the environment variable `ORNARE_DIAGNOSTIC_SESSION=1`.
- When enabled, entries are also written under
  `%LOCALAPPDATA%\Ornare\diagnostics\`, capped at 512 KB.
- The session is temporary: it does not persist across launches unless the variable is set
  again.

## Redaction

Two regular expressions remove identifying path information:

- `AbsolutePathRegex` matches drive-rooted and UNC paths.
- `UserSegmentRegex` matches the account segment under `Users` or `home`.

Redaction happens before an entry enters the buffer or file, so an unredacted copy is never
retained.

## Error channels

- User-visible copy comes from `AppResources` and is localized.
- Technical detail comes from `OperationResult.DiagnosticDetail` and is written to the
  diagnostic log or, for the CLI, to stderr.
- The CLI guarantees stdout carries exactly one JSON document under `--json`; diagnostics go
  to stderr only (INV-DATA-004).

## User-facing diagnostic report

`ExportDiagnosticReport` (`ManagerWindow`) writes a report the user can share. It contains the
redacted diagnostic log, app version, architecture and OS version, and never contains user
folder paths, image content, secrets or the SQLite database itself.
