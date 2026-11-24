# Ornare - Secure Development Lifecycle

Ornare handles user-chosen folders, image files and a local database. A defect can damage user
data or expose local information on a shared machine. The Secure SDLC defines the checks that
apply at each stage.

## Threat model

The living threat model is `docs/THREAT_MODEL.md`. Any change that adds an attack surface (new
file parser, new native interop, new input channel, new persistence) requires an update there
before merge.

## Design phase

- Identify the asset being protected (user folders, `desktop.ini`, the SQLite store, imported
  images, diagnostic output).
- Identify the trust boundary crossed (untrusted image bytes, Explorer activation arguments,
  a synced cloud folder, another process writing `desktop.ini`).
- Choose a fail-safe outcome for each failure path: refuse rather than corrupt.

## Implementation phase

- All external input is validated before use: image size, ICO frame count, SVG scripting,
  path format, CLI arguments.
- SQL is parameterized. Interpolated identifiers are compile-time constants and guarded.
- Filesystem writes are atomic (INV-FS-002) and confined to Ornare-owned locations.
- Native calls check their return value; unexpected HRESULTs are logged as diagnostic detail.
- Secrets never exist in the product: there is no account, token or key.

## Review phase

- Every change to filesystem mutation, SQLite, native interop or packaging receives an
  independent review against this document and the invariants.
- Reviewers assume every input is adversarial.
- A change that weakens a gate is rejected unless an ADR explicitly accepts the risk.

## Verification phase

- Dependency admission follows `docs/OSS_REUSE.md`.
- Malformed image corpora (`MalformedImageCorpusTests`) and adversarial storage tests
  (`AdversarialStorageAndPersistenceTests`) pass.
- Fault injection (`FaultInjectionCrashRecoveryTests`) proves data is not lost on a crash.

## Release phase

- Packages are signed; the signing identity is verified before publish.
- WACK passes.
- The diagnostic report contains no user-identifying path and no image content.
- Release notes state any security-relevant change.

## Incident response

- A data-integrity defect is treated as a release blocker.
- A vulnerability report is acknowledged per `SECURITY.md`.
